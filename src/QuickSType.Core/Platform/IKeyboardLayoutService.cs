// ReSharper disable EventNeverSubscribedTo.Global
namespace QuickSType.Core.Platform;

/// <summary>
/// Port for detecting the foreground keyboard layout on the host OS.
/// Event-driven via native OS notifications — does not poll (D-05).
/// </summary>
public interface IKeyboardLayoutService
{
    /// <summary>
    /// Latest known foreground-window keyboard layout. Updated on native notification callback.
    /// </summary>
    InputLayout CurrentLayout { get; }

    /// <summary>
    /// Fires when the OS keyboard layout changes. Fires on the native callback thread —
    /// consumers MUST marshal to UI thread via <c>Dispatcher.UIThread.Post</c> (D-04).
    /// </summary>
    event Action<InputLayout>? LayoutChanged;
}
