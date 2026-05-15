using ManySpeech.SileroVad;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace QuickSType.Core.Transcribe;

/// <summary>
/// Silero VAD gate (D-09 through D-12, STREAM-02).
/// Uses ManySpeech.SileroVad OnlineVad for real inference when model is available,
/// falls back to RMS energy threshold otherwise.
/// </summary>
public sealed class VadGate : IDisposable
{
    public const int SileroWindowSamples = 512;
    public const int HangoverMs = 300;
    public const int MaxChunkMs = 1000;
    public const float DefaultThreshold = 0.5f;

    private readonly ILogger _log;
    private readonly float _threshold;
    private readonly int _sampleRate;

    private OnlineVad? _vad;
    private OnlineStream? _stream;
    private readonly bool _available;

    private readonly List<float> _windowBuffer = new(capacity: SileroWindowSamples * 2);
    private readonly List<float> _accumulator = new(capacity: 16000);
    private bool _hasSpeechStarted;
    private DateTimeOffset? _hangoverDeadline;
    private ReadOnlyMemory<float>? _pendingChunk;

    public VadGate(
        string? modelFilePath = null,
        int sampleRate = 16000,
        float threshold = DefaultThreshold,
        ILogger<VadGate>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _threshold = threshold;
        _sampleRate = sampleRate;

        var path = modelFilePath ?? DefaultModelPath();
        if (path is not null && File.Exists(path))
        {
            try
            {
                _vad = new OnlineVad(path, threshold: threshold, sampleRate: sampleRate);
                _stream = _vad.CreateOnlineStream();
                _available = true;
                _log.LogInformation("Silero VAD initialized from {Path}", path);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Silero VAD init failed at {Path}; using energy fallback", path);
                _vad?.Dispose();
                _vad = null;
                _stream = null;
            }
        }
        else
        {
            _log.LogWarning("Silero VAD model not found at {Path}; using energy fallback", path);
        }
    }

    public static string DefaultModelPath()
    {
        var appData = Environment.GetFolderPath(
            OperatingSystem.IsMacOS()
                ? Environment.SpecialFolder.ApplicationData
                : Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "QuickSType", "models", "silero-vad", "silero_vad.onnx");
    }

    public bool IsAvailable => _available;

    public ReadOnlyMemory<float>? Feed(ReadOnlyMemory<float> frame)
    {
        if (frame.IsEmpty) return null;

        var src = frame.Span;
        int cursor = 0;
        while (cursor < src.Length)
        {
            int need = SileroWindowSamples - _windowBuffer.Count;
            int take = Math.Min(need, src.Length - cursor);
            for (int i = 0; i < take; i++) _windowBuffer.Add(src[cursor + i]);
            cursor += take;

            if (_windowBuffer.Count == SileroWindowSamples)
            {
                bool isSpeech = RunVadOn512Samples(_windowBuffer);
                UpdateChunkState(isSpeech);
                _windowBuffer.Clear();

                if (_pendingChunk is { } pending)
                {
                    _pendingChunk = null;
                    return pending;
                }
            }
        }

        if (_hasSpeechStarted)
        {
            foreach (var s in src) _accumulator.Add(s);
        }

        return null;
    }

    private void UpdateChunkState(bool isSpeech)
    {
        if (!_hasSpeechStarted)
        {
            if (isSpeech) _hasSpeechStarted = true;
            return;
        }

        if (isSpeech)
            _hangoverDeadline = null;
        else
            _hangoverDeadline ??= DateTimeOffset.UtcNow.AddMilliseconds(HangoverMs);

        int maxSamples = _sampleRate * MaxChunkMs / 1000;
        bool forceFlush = _accumulator.Count >= maxSamples;
        bool hangoverElapsed = _hangoverDeadline.HasValue && DateTimeOffset.UtcNow > _hangoverDeadline.Value;

        if (forceFlush || hangoverElapsed)
        {
            _pendingChunk = _accumulator.ToArray();
            _accumulator.Clear();
            _hangoverDeadline = null;
            _hasSpeechStarted = false;
            _log.LogDebug("VAD chunk emitted: {Samples} samples (force={F}, hang={H})",
                ((ReadOnlyMemory<float>)_pendingChunk).Length, forceFlush, hangoverElapsed);
        }
    }

    private bool RunVadOn512Samples(List<float> window)
    {
        if (_available && _vad is not null && _stream is not null)
        {
            try
            {
                _stream.AddSamples(window.ToArray());
                var result = _vad.GetResult(_stream);
                return result.Segments?.Count > 0;
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Silero inference failed; using energy fallback for this window");
            }
        }
        return EnergyFallback(window);
    }

    private static bool EnergyFallback(List<float> window)
    {
        float sumSq = 0f;
        foreach (var s in window) sumSq += s * s;
        return MathF.Sqrt(sumSq / window.Count) > 0.01f;
    }

    public ReadOnlyMemory<float>? FinalFlush()
    {
        if (_accumulator.Count == 0) return null;
        var final = _accumulator.ToArray();
        _accumulator.Clear();
        _hangoverDeadline = null;
        _hasSpeechStarted = false;
        return final;
    }

    public void Reset()
    {
        _windowBuffer.Clear();
        _accumulator.Clear();
        _hangoverDeadline = null;
        _hasSpeechStarted = false;
        _pendingChunk = null;
        try { _stream?.InitStream(); }
        catch (Exception ex) { _log.LogDebug(ex, "OnlineStream.InitStream threw on Reset"); }
    }

    public void Dispose()
    {
        try { _stream?.Dispose(); } catch (Exception ex) { _log.LogDebug(ex, "OnlineStream dispose threw"); }
        try { _vad?.Dispose(); } catch (Exception ex) { _log.LogDebug(ex, "OnlineVad dispose threw"); }
    }
}
