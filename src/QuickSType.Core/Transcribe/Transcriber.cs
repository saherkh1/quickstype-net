using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Whisper.net;
using QuickSType.Core.Audio;
using QuickSType.Core.Config;

namespace QuickSType.Core.Transcribe;

public sealed class Transcriber : IDisposable
{
    private readonly ILogger _log;
    private WhisperFactory? _factory;
    private string? _loadedModelPath;
    private readonly object _lock = new();

    public Transcriber(ILogger<Transcriber>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public string? LoadedModelPath => _loadedModelPath;

    public void EnsureLoaded(string modelPath, bool useGpu = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (_factory is not null && _loadedModelPath == modelPath) return;
            // Re-check after acquiring the lock: a prior caller may have just finished
            // loading a different model on this token, and the caller may have cancelled
            // in the interim.
            cancellationToken.ThrowIfCancellationRequested();
            _factory?.Dispose();
            var options = new WhisperFactoryOptions { UseGpu = useGpu };
            _factory = WhisperFactory.FromPath(modelPath, options);
            _loadedModelPath = modelPath;
            _log.LogInformation("Loaded Whisper model from {Path}", modelPath);
        }
    }

    public async Task<string> TranscribeAsync(
        ReadOnlyMemory<float> samples,
        int sampleRate,
        AppConfig config,
        CancellationToken cancellationToken = default)
    {
        if (_factory is null)
            throw new InvalidOperationException("Model not loaded. Call EnsureLoaded() first.");

        if (samples.Length < sampleRate / 5)
        {
            _log.LogDebug("Audio too short ({Samples} samples); skipping transcription", samples.Length);
            return string.Empty;
        }

        var wavBytes = WavWriter.WritePcm16(samples.Span, sampleRate);
        await using var wav = new MemoryStream(wavBytes, writable: false);

        var builder = _factory.CreateBuilder()
            .WithProbabilities()
            .WithThreads(Math.Max(2, Environment.ProcessorCount / 2));

        var lang = config.ActiveLanguage;
        var validLang = !string.IsNullOrWhiteSpace(lang)
            && QuickSType.Core.Languages.Find(lang) is not null;
        builder = (config.AutoLanguage || !validLang)
            ? builder.WithLanguageDetection()
            : builder.WithLanguage(lang);

        var processor = builder.Build();

        var sb = new System.Text.StringBuilder();
        await foreach (var segment in processor.ProcessAsync(wav, cancellationToken))
        {
            sb.Append(segment.Text);
        }

        await processor.DisposeAsync();

        var text = sb.ToString().Trim();
        _log.LogDebug("Transcribed {Chars} chars", text.Length);
        return text;
    }

    public async Task PrewarmAsync(string modelPath, int sampleRate = 16000, CancellationToken ct = default)
    {
        EnsureLoaded(modelPath);
        var silent = new float[sampleRate / 2];
        var cfg = new AppConfig { ActiveLanguage = "en", AutoLanguage = false };
        try
        {
            await TranscribeAsync(silent, sampleRate, cfg, ct);
            _log.LogInformation("Whisper warm-up complete");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Whisper warm-up failed (non-fatal)");
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _factory = null;
    }
}
