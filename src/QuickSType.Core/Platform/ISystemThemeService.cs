// ReSharper disable EventNeverSubscribedTo.Global
namespace QuickSType.Core.Platform;

/// <summary>
/// Port for observing OS theme variant (light/dark) and accent color.
/// Independent native listener — does NOT wrap Avalonia's PlatformSettings (D-08).
/// Consumable from non-UI code.
/// </summary>
public interface ISystemThemeService
{
    /// <summary>
    /// Current OS theme (light/dark) and accent color. Read once at startup,
    /// updated on native notification callback.
    /// </summary>
    AppTheme Current { get; }

    /// <summary>
    /// Fires when the OS theme variant or accent color changes. Fires on the native
    /// callback thread — consumers MUST marshal to UI thread via
    /// <c>Dispatcher.UIThread.Post</c> (D-10).
    /// </summary>
    event Action<AppTheme>? Changed;
}
