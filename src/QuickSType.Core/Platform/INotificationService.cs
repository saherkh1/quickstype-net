namespace QuickSType.Core.Platform;

public interface INotificationService
{
    void Notify(string title, string message, string? subtitle = null);
    Task PlayStartAsync();
    Task PlayStopAsync();
}
