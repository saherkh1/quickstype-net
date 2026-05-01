# Architecture

QuickSType is a single C#/.NET 10 codebase that runs on macOS and Windows. It uses Avalonia for the UI shell and platform-specific P/Invoke adapters for OS integration. Transcription is done locally via [Whisper.net](https://github.com/sandrohanea/whisper.cpp) (a managed wrapper around `whisper.cpp`).

## Projects

```
QuickSType.Core              cross-platform logic; references API packages
QuickSType.Platform.Mac      P/Invoke against CoreGraphics + osascript shell-out
QuickSType.Platform.Windows  P/Invoke against user32.dll + registry
QuickSType.UI                Avalonia desktop app; entry point; runtime packages
QuickSType.Core.Tests        xunit
```

`Core` exposes interfaces (`IPasteService`, `IAutoLaunchService`, `INotificationService`, `IPermissionService`) so the platform projects do not need to reference each other. `UI/Composition/AppHost` picks the right platform implementation at startup based on `RuntimeInformation.IsOSPlatform`.

## Pipeline

1. `HotkeyService` (SharpHook / libuiohook) fires `Pressed` / `Released`.
2. `DictationEngine.OnHotkeyPressed` starts `IAudioCapture` (`PortAudioCapture`).
3. PortAudio callback enqueues `float[]` chunks (mono / 16 kHz / 1024-frame buffer).
4. On `Released`, the engine concatenates chunks, plays a stop sound, switches state to `Processing`.
5. `Transcriber` lazy-loads the Whisper.net model, wraps the float buffer in an in-memory PCM16 WAV, and runs `WhisperFactory.CreateBuilder().WithLanguage(...).Build().ProcessAsync(stream)`.
6. The transcribed string goes to `IPasteService.PasteAsync`, which preserves the clipboard, sets the new text, posts Cmd+V (Mac) or Ctrl+V (Win) via native APIs, sleeps 120 ms for the OS to settle the paste, and restores the previous clipboard.
7. A toast/notification is shown if `ShowNotifications` is on.

## Tray menu

`TrayService` builds an Avalonia `NativeMenu` containing:

- live state indicator (Idle / Recording / Transcribing)
- `Settings…`
- `Language ▶` submenu — Auto-detect plus one entry per enabled language with a check-mark on the active one. Clicking saves immediately and rebuilds the submenu.
- `About` and `Quit`

The submenu is rebuilt every time it changes so check-marks stay in sync with the model.

## Config

JSON at `~/Library/Application Support/QuickSType/config.json` (Mac) or `%APPDATA%\QuickSType\config.json` (Win). Uses `System.Text.Json` source generation (`ConfigJsonContext`) so it works under AOT.

If the new path is empty and `~/.config/quickstype/config.json` exists, `ConfigStore.Load` migrates the old Python schema (translating `alt_r` → `VcRightAlt`, `mlx-community/...` → `ggml-...`, etc.).

## Models

`whisper.cpp` ggml `.bin` files. Catalog in `ModelCatalog.cs`. Default is `ggml-large-v3-turbo-q5_0` (~570 MB). Models are downloaded on first run via `ModelDownloader` (resumable via HTTP `Range`) and stored in:

- Mac: `~/Library/Application Support/QuickSType/models/`
- Win: `%LOCALAPPDATA%\QuickSType\models\`

## AOT notes

- `Core` and `Platform.*` projects set `IsAotCompatible=true`.
- `UI` references `Whisper.net.Runtime`, plus `Whisper.net.Runtime.CoreML` (Mac) or `Whisper.net.Runtime.Cuda.Windows` (Win) so AOT publish copies the native libs to the output.
- All JSON serialization goes through `ConfigJsonContext` (source-gen).
- Avalonia is configured with `AvaloniaUseCompiledBindingsByDefault=true`.

## Known limitations

- macOS Accessibility permission requires a relaunch to take effect after granting. The wizard says so explicitly.
- Right Alt = AltGr on European keyboards; `HotkeyService` uses the user's choice but the wizard warns when the active layout is non-US.
- Windows low-level keyboard hooks silently drop keystrokes destined for elevated windows.
- `whisper.cpp` Metal is ~1.5–2× slower than `mlx-whisper` on M-series for large models. Default model is the q5 quant of turbo to compensate.
