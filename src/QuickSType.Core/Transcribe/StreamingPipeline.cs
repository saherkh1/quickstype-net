using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Whisper.net;
using QuickSType.Core.Config;

namespace QuickSType.Core.Transcribe;

public sealed class StreamingPipeline : IStreamingTranscriber
{
    public const int DegradeStreakThreshold = 3;
    public const int DegradeLatencyMs = 800;

    private readonly ILogger _log;
    private readonly VadGate _vad;
    private readonly HallucinationFilter _filter;
    private readonly LocalAgreementMerger _merger;
    private WhisperFactory? _factory;
    private string? _loadedModelPath;
    private readonly object _lock = new();

    public event Action? DegradeRequested;

    public StreamingPipeline(VadGate vad, HallucinationFilter filter, ILogger<StreamingPipeline>? log = null)
    {
        _vad = vad ?? throw new ArgumentNullException(nameof(vad));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        _merger = new LocalAgreementMerger();
        _log = (ILogger?)log ?? NullLogger.Instance;
    }

    public void EnsureLoaded(string modelPath, bool useGpu = true)
    {
        lock (_lock)
        {
            if (_factory is not null && _loadedModelPath == modelPath) return;
            _factory?.Dispose();
            var options = new WhisperFactoryOptions { UseGpu = useGpu };
            _factory = WhisperFactory.FromPath(modelPath, options);
            _loadedModelPath = modelPath;
            _log.LogInformation("StreamingPipeline loaded Whisper model from {Path}", modelPath);
        }
    }

    public async IAsyncEnumerable<TranscriptUpdate> RunAsync(
        ChannelReader<ReadOnlyMemory<float>> frames,
        int sampleRate,
        AppConfig config,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_factory is null)
            throw new InvalidOperationException("StreamingPipeline: model not loaded. Call EnsureLoaded() first.");

        _merger.Reset();
        _vad.Reset();
        int slowStreak = 0;

        await foreach (var frame in frames.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = _vad.Feed(frame);
            if (chunk is null) continue;

            var startTicks = DateTime.UtcNow.Ticks;
            var update = await DecodeChunkAsync(chunk.Value, sampleRate, config, cancellationToken).ConfigureAwait(false);
            long latencyMs = (DateTime.UtcNow.Ticks - startTicks) / TimeSpan.TicksPerMillisecond;

            if (update is { } u && (u.RetractChars != 0 || u.AppendText.Length != 0))
                yield return u;

            slowStreak = latencyMs > DegradeLatencyMs ? slowStreak + 1 : 0;
            if (slowStreak >= DegradeStreakThreshold)
            {
                _log.LogWarning("Auto-degrade: {N} consecutive chunks > {Ms}ms", slowStreak, DegradeLatencyMs);
                try { DegradeRequested?.Invoke(); }
                catch (Exception ex) { _log.LogError(ex, "DegradeRequested handler threw"); }
                slowStreak = 0;
            }
        }

        var tail = _vad.FinalFlush();
        if (tail is { } final && final.Length >= sampleRate / 5)
        {
            var finalUpdate = await DecodeChunkAsync(final, sampleRate, config, cancellationToken).ConfigureAwait(false);
            if (finalUpdate is { } fu && (fu.RetractChars != 0 || fu.AppendText.Length != 0))
                yield return fu;
        }
    }

    private async Task<TranscriptUpdate?> DecodeChunkAsync(
        ReadOnlyMemory<float> chunk, int sampleRate, AppConfig config, CancellationToken ct)
    {
        if (_factory is null) return null;

        var wavBytes = Audio.WavWriter.WritePcm16(chunk.Span, sampleRate);
        await using var wav = new MemoryStream(wavBytes, writable: false);

        var builder = _factory.CreateBuilder()
            .WithProbabilities()
            .WithThreads(Math.Max(2, Environment.ProcessorCount / 2));

        var hint = ResolveLanguageHint(config);
        builder = string.IsNullOrEmpty(hint) ? builder.WithLanguageDetection() : builder.WithLanguage(hint);

        var processor = builder.Build();

        var sb = new System.Text.StringBuilder();
        float totalNoSpeech = 0f;
        int segCount = 0;
        await foreach (var seg in processor.ProcessAsync(wav, ct).ConfigureAwait(false))
        {
            sb.Append(seg.Text);
            totalNoSpeech += seg.NoSpeechProbability;
            segCount++;
        }
        await processor.DisposeAsync();

        float avgNoSpeech = segCount == 0 ? 1f : totalNoSpeech / segCount;
        var text = sb.ToString().Trim();

        if (_filter.ShouldDrop(text, avgNoSpeech))
        {
            _log.LogDebug("Chunk filtered (avgNoSpeech={Avg})", avgNoSpeech);
            return null;
        }
        return _merger.Merge(text);
    }

    private static string? ResolveLanguageHint(AppConfig config)
    {
        if (config.AutoLanguage) return null;
        var lang = config.ActiveLanguage;
        if (string.IsNullOrWhiteSpace(lang)) return null;
        return Languages.Find(lang) is not null ? lang : null;
    }

    public void Dispose()
    {
        try { _vad.Dispose(); } catch (Exception ex) { _log.LogDebug(ex, "VadGate dispose threw"); }
        try { _factory?.Dispose(); _factory = null; } catch (Exception ex) { _log.LogDebug(ex, "WhisperFactory dispose threw"); }
    }
}
