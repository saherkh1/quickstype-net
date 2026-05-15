using System.Threading.Channels;
using QuickSType.Core.Config;

namespace QuickSType.Core.Transcribe;

public interface IStreamingTranscriber : IDisposable
{
    IAsyncEnumerable<TranscriptUpdate> RunAsync(
        ChannelReader<ReadOnlyMemory<float>> frames,
        int sampleRate,
        AppConfig config,
        CancellationToken cancellationToken = default);

    event Action? DegradeRequested;
}
