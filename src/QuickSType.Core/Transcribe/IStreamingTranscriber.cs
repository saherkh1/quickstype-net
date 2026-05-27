using System.Threading.Channels;
using QuickSType.Core.Config;

namespace QuickSType.Core.Transcribe;

public interface IStreamingTranscriber : IDisposable
{
    void EnsureLoaded(string modelPath, bool useGpu = true);

    IAsyncEnumerable<TranscriptUpdate> RunAsync(
        ChannelReader<ReadOnlyMemory<float>> frames,
        int sampleRate,
        AppConfig config,
        CancellationToken cancellationToken = default);

    event Action? DegradeRequested;
}
