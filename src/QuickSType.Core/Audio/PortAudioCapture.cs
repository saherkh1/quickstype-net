using System.Buffers;
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

        // Rent a pooled buffer for the streaming channel.
        var rented = ArrayPool<float>.Shared.Rent(totalSamples);
        legacy.CopyTo(rented, 0);
        var slice = new ReadOnlyMemory<float>(rented, 0, totalSamples);
        if (_channel is not null && !_channel.Writer.TryWrite(slice))
        {
            // TryWrite returns false when the channel is full (DropOldest handles oldest);
            // still need to return the rented buffer since no consumer will own it.
            ArrayPool<float>.Shared.Return(rented);
        }

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
