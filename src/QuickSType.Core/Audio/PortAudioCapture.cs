using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PortAudioSharp;

namespace QuickSType.Core.Audio;

public sealed class PortAudioCapture : IAudioCapture
{
    private const int FramesPerBuffer = 1024;
    private const int BufferSeconds = 2;
    private const int FrameMs = 64;
    private static readonly object InitLock = new();
    private static bool _initialized;

    private readonly ILogger _log;
    private readonly ConcurrentQueue<float[]> _chunks = new();
    private Channel<ReadOnlyMemory<float>>? _channel;
    private PortAudioSharp.Stream? _stream;
    private int _deviceIndex;
    private string? _selectedDeviceName;
    private volatile bool _isRecording;

    public int SampleRate { get; }
    public int Channels { get; }
    public bool IsRecording => _isRecording;
    public ChannelReader<ReadOnlyMemory<float>>? Frames => _channel?.Reader;

    /// <inheritdoc />
    public event Action<float>? LevelChanged;

    public PortAudioCapture(ILogger<PortAudioCapture>? log = null, int sampleRate = 16000, int channels = 1)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        SampleRate = sampleRate;
        Channels = channels;
        EnsureInitialized();
        _deviceIndex = PortAudio.DefaultInputDevice;
    }

    private static void EnsureInitialized()
    {
        lock (InitLock)
        {
            if (_initialized) return;
            PortAudio.Initialize();
            _initialized = true;
        }
    }

    public IReadOnlyList<AudioDeviceInfo> ListInputDevices()
    {
        EnsureInitialized();
        var count = PortAudio.DeviceCount;
        var list = new List<AudioDeviceInfo>(count);
        for (int i = 0; i < count; i++)
        {
            var info = PortAudio.GetDeviceInfo(i);
            if (info.maxInputChannels > 0)
            {
                list.Add(new AudioDeviceInfo(i, info.name, info.maxInputChannels, info.defaultSampleRate));
            }
        }
        return list;
    }

    public void SelectInputDevice(string? deviceName)
    {
        _selectedDeviceName = deviceName;
        if (string.IsNullOrEmpty(deviceName))
        {
            _deviceIndex = PortAudio.DefaultInputDevice;
            return;
        }
        var devices = ListInputDevices();
        var match = devices.FirstOrDefault(d => string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
        _deviceIndex = match.Index >= 0 ? match.Index : PortAudio.DefaultInputDevice;
    }

    public void Start()
    {
        if (_isRecording) return;

        while (_chunks.TryDequeue(out _)) { }

        _channel = Channel.CreateBounded<ReadOnlyMemory<float>>(new BoundedChannelOptions(
            capacity: BufferSeconds * 1000 / FrameMs)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

        var inputParams = new StreamParameters
        {
            device = _deviceIndex,
            channelCount = Channels,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = PortAudio.GetDeviceInfo(_deviceIndex).defaultLowInputLatency,
            hostApiSpecificStreamInfo = IntPtr.Zero,
        };

        _stream = new PortAudioSharp.Stream(
            inParams: inputParams,
            outParams: null,
            sampleRate: SampleRate,
            framesPerBuffer: FramesPerBuffer,
            streamFlags: StreamFlags.NoFlag,
            callback: OnAudio,
            userData: IntPtr.Zero);

        _stream.Start();
        _isRecording = true;
        _log.LogDebug("Audio capture started (device {Idx} {Name})", _deviceIndex, _selectedDeviceName ?? "default");
    }

    public float[] Stop()
    {
        if (!_isRecording) return [];

        try { _channel?.Writer.TryComplete(); }
        catch (Exception ex) { _log.LogDebug(ex, "Channel writer complete threw"); }

        try
        {
            _stream?.Stop();
            _stream?.Dispose();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Error stopping PortAudio stream");
        }
        finally
        {
            _stream = null;
            _isRecording = false;
        }

        _channel = null;

        // Flatten subscribers' visuals immediately on teardown.
        try { LevelChanged?.Invoke(0f); }
        catch (Exception ex) { _log.LogDebug(ex, "LevelChanged(0) on Stop threw"); }

        return DrainChunks();
    }

    private float[] DrainChunks()
    {
        var total = 0;
        var buffers = new List<float[]>();
        while (_chunks.TryDequeue(out var chunk))
        {
            buffers.Add(chunk);
            total += chunk.Length;
        }
        var result = new float[total];
        var offset = 0;
        foreach (var b in buffers)
        {
            Array.Copy(b, 0, result, offset, b.Length);
            offset += b.Length;
        }
        return result;
    }

    private StreamCallbackResult OnAudio(
        IntPtr input, IntPtr output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags, IntPtr userData)
    {
        if (input == IntPtr.Zero) return StreamCallbackResult.Continue;
        var totalSamples = (int)frameCount * Channels;
        var legacy = new float[totalSamples];
        unsafe
        {
            var src = (float*)input.ToPointer();
            for (int i = 0; i < totalSamples; i++) legacy[i] = src[i];
        }
        _chunks.Enqueue(legacy);

        // Compute normalised RMS for this buffer and fan out to subscribers.
        // Audio-thread discipline: must never throw, never block. No allocations in this path
        // (legacy is the buffer we just filled; AudioLevelCalculator is allocation-free).
        try
        {
            var level = AudioLevelCalculator.ComputeNormalisedRms(legacy);
            LevelChanged?.Invoke(level);
        }
        catch (Exception ex)
        {
            // Never let a subscriber exception escape the audio callback. Log at Debug only —
            // this fires per audio buffer (~16/sec), so Warning would spam.
            _log.LogDebug(ex, "LevelChanged handler threw on audio thread");
        }

        // The queued array is immutable after this point, so the streaming reader can
        // observe the same buffer without an extra copy or ArrayPool ownership handoff.
        _channel?.Writer.TryWrite(new ReadOnlyMemory<float>(legacy, 0, totalSamples));

        return StreamCallbackResult.Continue;
    }

    public void Dispose()
    {
        try
        {
            if (_isRecording) Stop();
        }
        catch (Exception ex) { _log.LogDebug(ex, "PortAudioCapture dispose threw"); }
    }
}
