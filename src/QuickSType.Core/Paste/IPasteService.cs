namespace QuickSType.Core.Paste;

public interface IPasteService
{
    Task PasteAsync(string text, CancellationToken cancellationToken = default);
}
