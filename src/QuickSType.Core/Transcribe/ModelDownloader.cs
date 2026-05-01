using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace QuickSType.Core.Transcribe;

public sealed class ModelDownloader
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromMinutes(60),
    };

    private readonly ILogger _log;

    public ModelDownloader(ILogger<ModelDownloader>? log = null)
    {
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
        var dir = ModelCatalog.ModelsDirectory();
        Directory.CreateDirectory(dir);
        var finalPath = ModelCatalog.PathFor(model.Id);
        var partPath = finalPath + ".part";

        long resumeFrom = 0;
        if (File.Exists(partPath))
        {
            resumeFrom = new FileInfo(partPath).Length;
            _log.LogInformation("Resuming model download from {Bytes} bytes", resumeFrom);
        }

        if (File.Exists(finalPath))
        {
            _log.LogInformation("Model already installed at {Path}", finalPath);
            return finalPath;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, model.Url);
        if (resumeFrom > 0)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeFrom, null);
        }

        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? model.ApproxSizeBytes;
        if (resumeFrom > 0 && response.Content.Headers.ContentRange?.Length is long full)
        {
            total = full;
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
        File.Move(partPath, finalPath, overwrite: true);
        progress?.Report(new Progress(downloaded, total, 0));
        _log.LogInformation("Downloaded model {Id} to {Path}", model.Id, finalPath);
        return finalPath;
    }
}
