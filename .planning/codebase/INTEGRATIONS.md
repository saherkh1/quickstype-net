---
type: codebase-map
focus: tech
last_mapped: 2026-05-07
---

# QuickSType.NET — Integrations

QuickSType is a fully offline, local-first desktop app. It does **no** authentication, RPC, or telemetry over the network. The only outbound network traffic is the one-time download of a Whisper model from Hugging Face. Everything else (audio capture, transcription, paste, notifications, hotkey, autostart) is implemented through native OS APIs.

## External APIs / Services

- **Hugging Face (whisper.cpp model bucket)** — only outbound HTTP. Used by `src/QuickSType.Core/Transcribe/ModelDownloader.cs` to fetch ggml `.bin` model files. Base URL `https://huggingface.co/ggerganov/whisper.cpp/resolve/main` is hardcoded in `src/QuickSType.Core/Transcribe/ModelCatalog.cs:12` (`private const string Hf = ...`). Each `ModelInfo.Url` is `{Hf}/<modelId>.bin`. The download is a single `HttpClient.SendAsync` with `Range` header for resume; redirects are followed (`SocketsHttpHandler { AllowAutoRedirect = true }`). One global `HttpClient` instance, 60-minute timeout, no auth, no User-Agent customisation.
- **Otherwise**: None. There is no analytics, no auto-update server, no crash-reporting endpoint, no licensing service. Transcription happens entirely on-device via `whisper.cpp`.

## Local System Integrations

### Global hotkey

- `src/QuickSType.Core/Hotkey/HotkeyService.cs` wraps `SharpHook.TaskPoolGlobalHook` (libuiohook native lib bundled in the SharpHook NuGet). Listens for `KeyPressed` / `KeyReleased`, fires `Pressed` / `Released` events on a single configured `KeyCode`. Supports rebinding via `CaptureNextKeyAsync`. Defaults to `VcRightAlt`.
- libuiohook needs **macOS Input Monitoring** permission and **Accessibility** permission for synthesizing paste events.

### Clipboard

- `TextCopy.ClipboardService` is used cross-platform. Both `MacPasteService.PasteAsync` (`src/QuickSType.Platform.Mac/MacPasteService.cs:25-50`) and `WindowsPasteService.PasteAsync` (`src/QuickSType.Platform.Windows/WindowsPasteService.cs:25-50`) save the previous clipboard, set the transcribed text, synthesize a paste keystroke, sleep `PasteSettleMs = 120 ms`, then restore the previous clipboard.

### Synthesizing paste keystroke

- **macOS** (`src/QuickSType.Platform.Mac/MacPasteService.cs`): P/Invoke into `/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices` for `CGEventSourceCreate`, `CGEventCreateKeyboardEvent`, `CGEventSetFlags`, `CGEventPost`, plus `CFRelease` from `CoreFoundation`. Posts virtual keycode `0x09` (V) with the Command modifier. Fallback: shells out to `/usr/bin/osascript -e 'tell application "System Events" to keystroke "v" using {command down}'`.
- **Windows** (`src/QuickSType.Platform.Windows/WindowsPasteService.cs`): P/Invoke into `user32.dll` for `SendInput`, four `INPUT` structs (Ctrl down, V down, V up, Ctrl up).

### Permissions / Privacy panes

- **macOS** (`src/QuickSType.Platform.Mac/MacPermissions.cs`): `AXIsProcessTrusted` + `AXIsProcessTrustedWithOptions` from ApplicationServices to query Accessibility trust. Builds a `CFDictionary` with the `AXTrustedCheckOptionPrompt` key and `kCFBooleanTrue` value (loaded dynamically via `NativeLibrary.Load`/`GetExport` against CoreFoundation) to trigger the system prompt. Opens Privacy panes via `/usr/bin/open x-apple.systempreferences:com.apple.preference.security?Privacy_<Microphone|ListenEvent|Accessibility>`.
- **Windows** (`src/QuickSType.Platform.Windows/WindowsPermissions.cs`): all checks return `true`; `Open*Settings` shells out to `ms-settings:` URIs.

### Notifications & sounds

- **macOS** (`src/QuickSType.Platform.Mac/MacNotifications.cs`): toasts via `/usr/bin/osascript -e 'display notification "..." with title "..."'`. Sounds via `/usr/bin/afplay` reading `/System/Library/Sounds/Tink.aiff` (start) and `/System/Library/Sounds/Pop.aiff` (stop).
- **Windows** (`src/QuickSType.Platform.Windows/WindowsNotifications.cs`): toasts via a generated PowerShell script that creates a `System.Windows.Forms.NotifyIcon` and calls `ShowBalloonTip`. Sounds via `kernel32.Beep(880, 80)` / `Beep(440, 80)` (P/Invoke).

### Auto-launch on login

- **macOS** (`src/QuickSType.Platform.Mac/MacAutoLaunch.cs`): writes a LaunchAgent plist to `~/Library/LaunchAgents/com.saherk.quickstype.plist` and runs `/bin/launchctl load|unload <plist>`. Label `com.saherk.quickstype`.
- **Windows** (`src/QuickSType.Platform.Windows/WindowsAutoLaunch.cs`): writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\QuickSType` = `"<exe>" --tray` via `Microsoft.Win32.Registry`.

### Tray / menu-bar icon

- `src/QuickSType.UI/Tray/TrayService.cs` builds an Avalonia `TrayIcon` + `NativeMenu`. State icon swaps between `tray-idle.png`, `tray-recording.png`, `tray-processing.png` from `Avalonia.Platform.AssetLoader` URIs `avares://QuickSType/Assets/tray-<state>.png`. On macOS the same `NativeMenu` instance is mutated in place (rather than reassigned) because AppKit's NSMenu cache doesn't reliably update on `Menu` reassignment (see comment at `TrayService.cs:65-69`).

## Audio / Hardware

- **Capture stack**: `PortAudioSharp2` 1.0.6 (managed wrapper around the native PortAudio library). The native binaries ship per-RID via `org.k2fsa.portaudio.runtime.osx-arm64` / `…osx-x64` / `…win-x64` NuGets gated by `$([MSBuild]::IsOSPlatform(...))` conditions in `src/QuickSType.UI/QuickSType.UI.csproj` lines 25-27. PortAudio internally uses **CoreAudio** on macOS and **WASAPI/MME** on Windows — QuickSType doesn't talk to those host APIs directly.
- **Capture interface**: `src/QuickSType.Core/Audio/IAudioCapture.cs` (`Start`, `Stop`, `ListInputDevices`, `SelectInputDevice`).
- **Implementation**: `src/QuickSType.Core/Audio/PortAudioCapture.cs`. Single static `PortAudio.Initialize()` (idempotent under a lock). Opens a `PortAudioSharp.Stream` with `Float32`, mono, **16 kHz**, 1024-frame buffer. Audio arrives in a `StreamCallback` (line 143) that copies the float buffer out of the unmanaged pointer (`unsafe` block — note `AllowUnsafeBlocks=true` on the Core csproj) and enqueues it on a `ConcurrentQueue<float[]>`. `Stop()` drains and concatenates into one `float[]`.
- **Encoding for whisper**: `src/QuickSType.Core/Audio/WavWriter.cs` writes a PCM16 WAV header in-memory and clamps each `float` sample to `[-1, 1]` before scaling by `short.MaxValue`. The resulting `MemoryStream` is fed to `WhisperFactory.CreateBuilder().Build().ProcessAsync(stream)` in `Transcriber.cs:65-71`.

## Model / ML pipelines

- **Library**: `Whisper.net` 1.9.0 (managed C# wrapper around `whisper.cpp`). `Whisper.net.Runtime` 1.9.0 ships the native `whisper.cpp` binary; `Whisper.net.Runtime.CoreML` (Mac) and `Whisper.net.Runtime.Cuda.Windows` (Win) ship platform-specific accelerated builds (Metal + CoreML on Apple Silicon, CUDA on Windows GPUs).
- **Model files**: ggml `.bin` format from `https://huggingface.co/ggerganov/whisper.cpp/resolve/main`. Catalog (`src/QuickSType.Core/Transcribe/ModelCatalog.cs:14-23`) lists 7 variants: `ggml-tiny`, `ggml-base`, `ggml-small`, `ggml-medium`, `ggml-large-v3`, `ggml-large-v3-turbo`, `ggml-large-v3-turbo-q5_0`. Default surfaced to the user is `ggml-large-v3-turbo-q5_0` (the recommended quantised turbo, ~570 MB). `AppConfig.Model` defaults to `ggml-base` for first-launch safety.
- **Download** (`src/QuickSType.Core/Transcribe/ModelDownloader.cs`): standard `HttpClient` GET with optional `Range` header for resume from a `<modelId>.bin.part` file. `IProgress<Progress>` reports bytes/sec and percent every 0.5 s. On completion `<modelId>.bin.part` is moved to `<modelId>.bin`. No checksum verification.
- **Inference** (`src/QuickSType.Core/Transcribe/Transcriber.cs`):
  - `EnsureLoaded(modelPath, useGpu=true)` builds a singleton `WhisperFactory.FromPath(modelPath, new WhisperFactoryOptions { UseGpu = useGpu })`. `useGpu` is set to `_config.TranscriptionBackend != "cpu"` in `DictationEngine.ProcessAsync` (`src/QuickSType.Core/DictationEngine.cs:123`).
  - `TranscribeAsync` builds a processor with `WithProbabilities()`, `WithThreads(max(2, ProcessorCount/2))`, then either `WithLanguageDetection()` (when `AutoLanguage` is on or the configured code is invalid) or `WithLanguage(activeLanguage)` (`Transcriber.cs:54-63`).
  - `PrewarmAsync` runs a half-second silent buffer through the pipeline to JIT the native code path.
- **Languages catalog**: `src/QuickSType.Core/Languages.cs` — 16 codes (en, he, ar, es, fr, de, it, pt, ru, ja, ko, zh, nl, pl, tr, hi).

## Filesystem / OS Storage

Resolved by `ConfigStore.DefaultPath()` (`src/QuickSType.Core/Config/ConfigStore.cs:25`) and `ModelCatalog.ModelsDirectory()` (`src/QuickSType.Core/Transcribe/ModelCatalog.cs:30`).

| Asset | macOS | Windows | Linux fallback |
|---|---|---|---|
| Config file | `~/Library/Application Support/QuickSType/config.json` | `%APPDATA%\QuickSType\config.json` | `$XDG_CONFIG_HOME/quickstype/config.json` (else `~/.config/quickstype/config.json`) |
| Model cache | `~/Library/Application Support/QuickSType/models/<id>.bin` | `%LOCALAPPDATA%\QuickSType\models\<id>.bin` | `$XDG_DATA_HOME/quickstype/models/` (else `~/.local/share/quickstype/models/`) |
| Auto-launch | `~/Library/LaunchAgents/com.saherk.quickstype.plist` | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\QuickSType` | n/a |
| Legacy Python config (read-only, migrated) | `~/.config/quickstype/config.json` | `~/.config/quickstype/config.json` | same |
| App icon (Mac bundle) | `QuickSType.app/Contents/Resources/AppIcon.png` | n/a | n/a |
| Tray icons | bundled as Avalonia resources `avares://QuickSType/Assets/tray-*.png` (no on-disk path) |  |  |

There is no log directory. Logging is `Microsoft.Extensions.Logging.Console` only — output goes to stdout/stderr (`src/QuickSType.UI/Composition/AppHost.cs:58-62`).

## Telemetry / Crash reporting

None. There is no Sentry/AppInsights/Mixpanel SDK. The only crash handling is the top-level `try/catch` in `Program.Main` (`src/QuickSType.UI/Program.cs:10-18`) which writes the exception to `Console.Error` and exits with code 1. Domain errors are surfaced via `DictationEngine.Errored` events to UI consumers and via `_log.LogError` to the console.

## Summary

QuickSType's only "integration" with anything off-device is the single Hugging Face model download. Everything else is OS-native: PortAudio for capture, libuiohook (via SharpHook) for the global hotkey, CoreGraphics/`user32` for synthesizing paste, ApplicationServices/Win32 settings for permissions, launchctl/Run-key for autostart, and Avalonia's tray icon for the menu-bar UI.
