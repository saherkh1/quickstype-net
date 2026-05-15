using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuickSType.Core.Config;

namespace QuickSType.Core.Transcribe;

public sealed class ModelDownloader
{
    private static readonly HttpClient DefaultHttp = new(new SocketsHttpHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromMinutes(60),
    };

    private readonly HttpClient _http;
    private readonly ILogger _log;

    public ModelDownloader(ILogger<ModelDownloader>? log = null)
        : this(DefaultHttp, log)
    {
    }

    internal ModelDownloader(HttpClient http, ILogger<ModelDownloader>? log = null)
    {
        _http = http;
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public sealed record Progress(long DownloadedBytes, long TotalBytes, double BytesPerSecond)
    {
        public double Percent => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100.0 : 0.0;
    }

    public async Task<string> DownloadAsync(
        ModelInfo model,
        IProgress<Progress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var manifest = ModelManifestLoader.Load();
        var expectedSha = manifest.GetSha256(model.Id);   // throws ArgumentException if missing
        var pinnedUrl = manifest.GetUrl(model.Id);

        var dir = ModelCatalog.ModelsDirectory();
        Directory.CreateDirectory(dir);
        var finalPath = ModelCatalog.PathFor(model.Id);
        var partPath = finalPath + ".part";

        if (File.Exists(finalPath))
        {
            _log.LogInformation("Model already installed at {Path}", finalPath);
            return finalPath;
        }

        long resumeFrom = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
        if (resumeFrom > 0) _log.LogInformation("Resuming model download from {Bytes} bytes", resumeFrom);

        using var request = new HttpRequestMessage(HttpMethod.Get, pinnedUrl);
        if (resumeFrom > 0)
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeFrom, null);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        // HARDEN-01 / Pitfall 3: server ignored Range — must restart from zero
        if (resumeFrom > 0 && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
        {
            _log.LogWarning("Server returned 200 to a Range request; restarting download from zero for {Id}", model.Id);
            if (File.Exists(partPath)) File.Delete(partPath);
            resumeFrom = 0;
        }

        var total = response.Content.Headers.ContentLength ?? model.ApproxSizeBytes;
        if (resumeFrom > 0 && response.Content.Headers.ContentRange?.Length is long full) total = full;

        using var hasher = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);

        // If resuming, we must re-hash the existing .part bytes BEFORE appending; otherwise the final SHA-256 is wrong.
        if (resumeFrom > 0)
        {
            await using var existing = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.None, 81920, useAsync: true);
            var rebuf = new byte[81920];
            while (true)
            {
                var rn = await existing.ReadAsync(rebuf, cancellationToken);
                if (rn == 0) break;
                hasher.AppendData(rebuf, 0, rn);
            }
        }

        await using var http = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = new FileStream(partPath, resumeFrom > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long downloaded = resumeFrom;
        var lastReport = DateTime.UtcNow;
        long lastBytes = downloaded;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = await http.ReadAsync(buffer, cancellationToken);
            if (n == 0) break;
            hasher.AppendData(buffer, 0, n);
            await file.WriteAsync(buffer.AsMemory(0, n), cancellationToken);
            downloaded += n;

            var now = DateTime.UtcNow;
            var elapsed = (now - lastReport).TotalSeconds;
            if (elapsed >= 0.5)
            {
                var bps = (downloaded - lastBytes) / elapsed;
                progress?.Report(new Progress(downloaded, total, bps));
                lastReport = now;
                lastBytes = downloaded;
            }
        }

        await file.FlushAsync(cancellationToken);
        file.Close();

        var actualBytes = hasher.GetHashAndReset();
        var actualHex = Convert.ToHexStringLower(actualBytes);
        var expectedBytes = Convert.FromHexString(expectedSha);

        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
        {
            File.Delete(partPath);
            _log.LogError("SHA-256 mismatch for {Id}: expected {Expected}, actual {Actual}", model.Id, expectedSha, actualHex);
            throw new ModelIntegrityException(model.Id, expectedSha, actualHex);
        }

        File.Move(partPath, finalPath, overwrite: true);
        progress?.Report(new Progress(downloaded, total, 0));
        _log.LogInformation("Downloaded model {Id} to {Path} (sha256 verified)", model.Id, finalPath);
        return finalPath;
    }

    public async Task<string> DownloadDenyListAsync(ModelInfo model, CancellationToken cancellationToken = default)
    {
        var dir = ModelCatalog.ModelsDirectory();
        Directory.CreateDirectory(dir);
        var finalPath = ModelCatalog.DenyListPathFor(model.Id);
        var tmpPath = finalPath + ".tmp";
        var url = ModelCatalog.DenyListUrlFor(model.Id);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode == 404)
            {
                _log.LogWarning("Hallucinations manifest not found for {Id} at {Url}; writing empty manifest", model.Id, url);
                await WriteEmptyManifestAsync(finalPath, tmpPath, cancellationToken).ConfigureAwait(false);
                return finalPath;
            }
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(tmpPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(tmpPath, finalPath, overwrite: true);
            _log.LogInformation("Downloaded deny-list for {Id} to {Path} ({Bytes} bytes)", model.Id, finalPath, bytes.Length);
            return finalPath;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Deny-list fetch failed for {Id}; falling back to empty manifest", model.Id);
            await WriteEmptyManifestAsync(finalPath, tmpPath, cancellationToken).ConfigureAwait(false);
            return finalPath;
        }
    }

    private static async Task WriteEmptyManifestAsync(string finalPath, string tmpPath, CancellationToken ct)
    {
        var manifest = new HallucinationManifest { Version = 1, Phrases = new List<string>() };
        var json = System.Text.Json.JsonSerializer.Serialize(manifest, ConfigJsonContext.Default.HallucinationManifest);
        await File.WriteAllTextAsync(tmpPath, json, ct).ConfigureAwait(false);
        File.Move(tmpPath, finalPath, overwrite: true);
    }
}
