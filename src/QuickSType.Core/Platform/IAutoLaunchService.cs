namespace QuickSType.Core.Platform;

public interface IAutoLaunchService
{
    bool IsEnabled();
    void SetEnabled(bool enabled, string? executablePath = null);
}
