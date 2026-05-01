namespace QuickSType.Core.Audio;

public interface IAudioCapture : IDisposable
{
    int SampleRate { get; }
    int Channels { get; }
    bool IsRecording { get; }

    void Start();
    float[] Stop();

    IReadOnlyList<AudioDeviceInfo> ListInputDevices();
    void SelectInputDevice(string? deviceName);
}

public readonly record struct AudioDeviceInfo(int Index, string Name, int MaxInputChannels, double DefaultSampleRate);
