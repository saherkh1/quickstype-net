using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SharpHook;
using SharpHook.Data;

namespace QuickSType.Core.Hotkey;

public sealed class HotkeyService : IDisposable
{
    private readonly ILogger _log;
    private readonly TaskPoolGlobalHook _hook;
    private KeyCode _hotkey;
    private bool _isHeld;
    private bool _captureNext;
    private TaskCompletionSource<KeyCode>? _captureTcs;

    public event Action? Pressed;
    public event Action? Released;

    public KeyCode Hotkey => _hotkey;

    public HotkeyService(string hotkeyName, ILogger<HotkeyService>? log = null)
    {
        _log = (ILogger?)log ?? NullLogger.Instance;
        _hotkey = ParseKey(hotkeyName);
        _hook = new TaskPoolGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    public Task RunAsync()
    {
        _log.LogInformation("Starting global hotkey hook for {Key}", _hotkey);
        return _hook.RunAsync();
    }

    public void SetHotkey(string hotkeyName)
    {
        _hotkey = ParseKey(hotkeyName);
        _log.LogInformation("Hotkey changed to {Key}", _hotkey);
    }

    public Task<KeyCode> CaptureNextKeyAsync(TimeSpan timeout)
    {
        _captureTcs = new TaskCompletionSource<KeyCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureNext = true;
        var tcs = _captureTcs;
        _ = Task.Delay(timeout).ContinueWith(_ =>
        {
            if (_captureNext)
            {
                _captureNext = false;
                tcs.TrySetCanceled();
            }
        });
        return tcs.Task;
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (_captureNext && !IsModifier(e.Data.KeyCode))
        {
            _captureNext = false;
            _captureTcs?.TrySetResult(e.Data.KeyCode);
            return;
        }

        if (e.Data.KeyCode == _hotkey && !_isHeld)
        {
            _isHeld = true;
            try { Pressed?.Invoke(); }
            catch (Exception ex) { _log.LogError(ex, "Pressed handler threw"); }
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode == _hotkey && _isHeld)
        {
            _isHeld = false;
            try { Released?.Invoke(); }
            catch (Exception ex) { _log.LogError(ex, "Released handler threw"); }
        }
    }

    private static bool IsModifier(KeyCode k) => k switch
    {
        KeyCode.VcLeftShift or KeyCode.VcRightShift
            or KeyCode.VcLeftControl or KeyCode.VcRightControl
            or KeyCode.VcLeftAlt or KeyCode.VcRightAlt
            or KeyCode.VcLeftMeta or KeyCode.VcRightMeta => true,
        _ => false,
    };

    public static KeyCode ParseKey(string name)
    {
        if (Enum.TryParse<KeyCode>(name, ignoreCase: true, out var k)) return k;
        return KeyCode.VcRightAlt;
    }

    public static string Format(KeyCode k) => k.ToString();

    public void Dispose()
    {
        try
        {
            _hook.KeyPressed -= OnKeyPressed;
            _hook.KeyReleased -= OnKeyReleased;
            _hook.Dispose();
        }
        catch { /* swallow on dispose */ }
    }
}
