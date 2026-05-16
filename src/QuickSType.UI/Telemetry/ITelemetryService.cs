namespace QuickSType.UI.Telemetry;

public interface ITelemetryService : IDisposable
{
    bool IsEnabled { get; }

    void CaptureException(Exception exception, IReadOnlyDictionary<string, string>? context = null);

    void AddBreadcrumb(string message, IReadOnlyDictionary<string, string>? data = null);
}
