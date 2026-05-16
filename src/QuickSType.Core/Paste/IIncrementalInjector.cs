using QuickSType.Core.Transcribe;

namespace QuickSType.Core.Paste;

public interface IIncrementalInjector
{
    Task ApplyAsync(TranscriptUpdate update, CancellationToken cancellationToken = default);
}
