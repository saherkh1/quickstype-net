namespace QuickSType.Core.History;

public interface IHistoryService
{
    Task AppendAsync(HistoryEntry entry, CancellationToken ct = default);
}
