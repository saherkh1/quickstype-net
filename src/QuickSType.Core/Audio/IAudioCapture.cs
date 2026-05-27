using System.Threading.Channels;

namespace QuickSType.Core.Audio;

public interface IAudioCapture : IDisposable
{
    int SampleRate { get; }
    int Channels { get; }
    bool IsRecording { get; }
    ChannelReader<ReadOnlyMemory<float>>? Frames { get; }

    void Start();
    float[] Stop();

    IReadOnlyList<AudioDeviceInfo> ListInputDevices();
    void SelectInputDevice(string? deviceName);

    /// <summary>
    /// Raised once per captured audio buffer with the normalised RMS level (0..1) of that buffer.
    /// IMPORTANT: invoked on the real-time audio callback thread. Handlers MUST NOT block,
    /// allocate heavily, lock contended resources, or marshal synchronously to the UI thread.
    /// Marshal to the UI dispatcher via a non-blocking Post.
    /// A final invocation with level=0 is fired from <see cref="Stop"/> so subscribers can
    /// flatten their visual state immediately.
    /// </summary>
    event Action<float>? LevelChanged;
}

public readonly record struct AudioDeviceInfo(int Index, string Name, int MaxInputChannels, double DefaultSampleRate);
