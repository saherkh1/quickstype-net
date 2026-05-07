---
type: codebase-map
focus: arch
last_mapped: 2026-05-07
---

# QuickSType.NET — Architecture

> Cross-reference: an existing high-level summary lives at `docs/ARCHITECTURE.md`. This document expands on it with code-level citations and runtime detail. Where they overlap, the existing doc is canonical for project intent and this one is canonical for "where in the source".

## High-level pattern

QuickSType is a **layered + ports-and-adapters (Hexagonal)** .NET solution. It has:

- A **pure core** (`QuickSType.Core`) that owns domain logic and defines OS-shaped *ports* as C# interfaces.
- Two **platform adapters** (`QuickSType.Platform.Mac`, `QuickSType.Platform.Windows`) that implement those ports via P/Invoke or shell-out.
- A **single composition root + UI host** (`QuickSType.UI`, an Avalonia desktop app) that picks the correct adapter at runtime and wires everything into the dictation pipeline.
- A **test ring** (`QuickSType.Core.Tests`) targeting only the core.

There is no IoC container; `QuickSType.UI/Composition/AppHost.cs` is a hand-rolled composition root using `RuntimeInformation.IsOSPlatform(...)` for adapter selection. Each platform adapter project references `Core` only — never each other — so the dependency graph stays acyclic.

The five projects are listed in `QuickSType.sln:3-12`. `Directory.Build.props:4-7` pins `net10.0` with nullable + implicit usings on; `global.json:1-7` pins SDK `10.0.100` with `latestFeature` roll-forward.

## Project / Layer breakdown

`<ProjectReference>` blocks read from each `.csproj`:

| Project | Role | Depends on |
|---|---|---|
| `QuickSType.Core` (`src/QuickSType.Core/QuickSType.Core.csproj`) | Pure cross-platform domain: hotkey wrapper, audio capture, Whisper transcription, model catalog/downloader, config schema + persistence, platform port interfaces. AOT-compatible. | NuGet only — `SharpHook`, `Whisper.net`, `PortAudioSharp2`, `TextCopy`, `Microsoft.Extensions.Logging.Abstractions`. No project refs. |
| `QuickSType.Platform.Mac` (`src/QuickSType.Platform.Mac/QuickSType.Platform.Mac.csproj`) | Mac adapters via P/Invoke (`CoreGraphics`, `ApplicationServices`, `CoreFoundation`) and shell-out to `osascript`/`afplay`/`launchctl`/`open`. | `QuickSType.Core` only. |
| `QuickSType.Platform.Windows` (`src/QuickSType.Platform.Windows/QuickSType.Platform.Windows.csproj`) | Windows adapters: `user32.SendInput` for paste, `kernel32.Beep` for sounds, `Microsoft.Win32.Registry` for autostart, `ms-settings:` URIs. | `QuickSType.Core` only. |
| `QuickSType.UI` (`src/QuickSType.UI/QuickSType.UI.csproj`) | Avalonia 11.2 desktop app. Composition root, tray, settings window, view models. Output `Exe` named `QuickSType`. Runtime native packages (Whisper, PortAudio, optional CoreML/CUDA) are gated by `$([MSBuild]::IsOSPlatform(...))` conditions on the package refs. | `QuickSType.Core` + **both** platform projects + Avalonia + `Whisper.net.Runtime` + `CommunityToolkit.Mvvm`. |
| `QuickSType.Core.Tests` (`tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj`) | xunit + Shouldly tests for `Core` only. | `QuickSType.Core`. |

The Mac/Windows projects intentionally do **not** reference each other. The UI project references both because it's the only place that decides at runtime which to instantiate (see next section).

## Platform abstraction contract

The ports are four interfaces in `src/QuickSType.Core/Platform/` plus one in `src/QuickSType.Core/Paste/`:

| Port | File | Adapters |
|---|---|---|
| `IPasteService` | `src/QuickSType.Core/Paste/IPasteService.cs` (single async method `PasteAsync(text, ct)`) | `MacPasteService` (`src/QuickSType.Platform.Mac/MacPasteService.cs`) — Cmd+V via `CGEventCreateKeyboardEvent` + `CGEventPost`, falls back to `osascript` keystroke. Saves+restores clipboard around a 120 ms settle. <br> `WindowsPasteService` (`src/QuickSType.Platform.Windows/WindowsPasteService.cs`) — same envelope but `user32.SendInput` for Ctrl+V. |
| `IPermissionService` | `src/QuickSType.Core/Platform/IPermissionService.cs` (mic / input-monitoring / accessibility checks + open-settings) | `MacPermissions` (`MacPermissions.cs`) — calls `AXIsProcessTrusted` / `AXIsProcessTrustedWithOptions`; opens `x-apple.systempreferences:` URIs via `/usr/bin/open`. <br> `WindowsPermissions` (`WindowsPermissions.cs`) — all checks return true (Windows mic prompt is OS-driven); opens `ms-settings:` URIs. |
| `IAutoLaunchService` | `src/QuickSType.Core/Platform/IAutoLaunchService.cs` | `MacAutoLaunch` — writes/removes a LaunchAgent plist at `~/Library/LaunchAgents/com.saherk.quickstype.plist` and `launchctl load/unload`. <br> `WindowsAutoLaunch` — `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `QuickSType`. |
| `INotificationService` | `src/QuickSType.Core/Platform/INotificationService.cs` (`Notify`, `PlayStartAsync`, `PlayStopAsync`) | `MacNotifications` — `osascript "display notification"` + `/usr/bin/afplay` of `Tink.aiff` / `Pop.aiff`. <br> `WindowsNotifications` — PowerShell `NotifyIcon` balloon + `kernel32.Beep`. |

### Composition / DI wiring

`AppHost.Create()` in `src/QuickSType.UI/Composition/AppHost.cs:56-105` is the only place that knows which adapter to bind:

- `OSX` branch instantiates the four `Platform.Mac.*` services (lines 76-82).
- `Windows` branch instantiates the four `Platform.Windows.*` services (lines 83-89).
- Otherwise it binds in-process **`Noop*`** stubs (`AppHost.cs:140-167`) so unit-style runs and unsupported platforms don't crash.

`AppHost` itself is plain constructor-injection: it constructs `LoggerFactory`, `ConfigStoreWithExists`, `PortAudioCapture`, `Transcriber`, `ModelDownloader`, `HotkeyService`, then `DictationEngine` with the resolved port instances, and finally subscribes `engine.OnHotkeyPressed/Released` to `hotkey.Pressed/Released` (`AppHost.cs:101-102`). It also raises `ConfigChanged` so the tray/settings VM can react when config is mutated (`AppHost.cs:107, 116-122`).

`SettingsViewModel` and `TrayService` consume `AppHost` directly rather than re-resolving services — there is no service locator.

## Data flow / runtime pipeline

End-to-end, hotkey to paste:

1. **Hotkey down.** `HotkeyService` (`src/QuickSType.Core/Hotkey/HotkeyService.cs`) wraps SharpHook's `TaskPoolGlobalHook`. `OnKeyPressed` (lines 59-74) compares `e.Data.KeyCode` to the configured `_hotkey`, debounces on `_isHeld`, and raises `Pressed`. The hotkey string from config is parsed by `ParseKey` (lines 95-99) defaulting to `KeyCode.VcRightAlt`.
2. **Engine starts capture.** `DictationEngine.OnHotkeyPressed` (`src/QuickSType.Core/DictationEngine.cs:65-84`) guards on `state == Idle`, calls `_audio.Start()`, transitions to `Recording`, fires `_notify.PlayStartAsync()`.
3. **Audio fills.** `PortAudioCapture.Start` (`src/QuickSType.Core/Audio/PortAudioCapture.cs:73-100`) opens a PortAudio stream (mono, 16 kHz, `Float32`, 1024-frame buffer). The native callback `OnAudio` (lines 143-158) runs on the PortAudio audio thread, copies the input pointer into a `float[]`, and enqueues onto a `ConcurrentQueue<float[]>`.
4. **Hotkey up.** `OnKeyReleased` raises `Released` → `DictationEngine.OnHotkeyReleased` (`DictationEngine.cs:86-101`) calls `_audio.Stop()` to drain the queue (`PortAudioCapture.cs:102-141`), plays the stop sound, transitions to `Processing`, and kicks off `ProcessAsync(samples, _cts.Token)` fire-and-forget.
5. **Process.** `DictationEngine.ProcessAsync` (`DictationEngine.cs:103-151`):
   - Drops if `<200 ms` of audio (line 107).
   - Resolves the model path via `ModelCatalog.PathFor` (`src/QuickSType.Core/Transcribe/ModelCatalog.cs:47-48`) and bails to `Errored` if missing.
   - Calls `_transcriber.EnsureLoaded(modelPath, useGpu)` — lazy, lock-protected `WhisperFactory.FromPath` in `Transcriber.cs:23-34`.
   - `Transcriber.TranscribeAsync` (`Transcriber.cs:36-78`) wraps the float buffer via `WavWriter.WritePcm16` into an in-memory PCM16 WAV, builds a Whisper processor with `WithLanguageDetection()` or `WithLanguage(...)` based on `AutoLanguage`, runs `processor.ProcessAsync(stream)`, concatenates segment text, returns trimmed string.
6. **Paste.** `await _paste.PasteAsync(text, ct)` dispatches to `MacPasteService`/`WindowsPasteService`. Each preserves the prior clipboard, sets the new text, synthesizes Cmd+V/Ctrl+V via native APIs, sleeps `PasteSettleMs = 120`, restores the clipboard.
7. **Notify + idle.** If `config.ShowNotifications`, fires `_notify.Notify(...)`. `finally` block sets state back to `Idle`.

State changes (`Idle`/`Recording`/`Processing`) are published via `DictationEngine.StateChanged`; `TrayService.OnStateChanged` (`src/QuickSType.UI/Tray/TrayService.cs:171-195`) marshals them onto `Dispatcher.UIThread` and swaps the tray icon + state label.

## Entry points

- **Process entry**: `src/QuickSType.UI/Program.cs:5-26`. `[STAThread] Main` calls `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)` inside a try/catch that prints to `stderr` and returns 1 on failure. `BuildAvaloniaApp` chains `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()`.
- **Avalonia bootstrap**: `src/QuickSType.UI/App.axaml.cs:14-34`. `Initialize` loads `App.axaml`. `OnFrameworkInitializationCompleted` is the **startup sequence**:
  1. `AppHost.Create()` — composition root runs (logger factory → config → audio/transcriber/downloader → platform adapters → hotkey + engine wired together).
  2. `new TrayService(host).Install(this)` — builds and registers the `TrayIcon` + `NativeMenu`.
  3. `desktop.ShutdownMode = OnExplicitShutdown` and `desktop.MainWindow = null` — app runs as menu-bar/tray only (`LSUIElement=true` in `build/Info.plist:25-26`).
  4. `_ = Host.StartAsync()` — calls `Hotkey.RunAsync()` (the SharpHook hook loop) without awaiting.
- **Settings window**: `src/QuickSType.UI/Views/MainWindow.axaml` + `.cs`. Lazy-instantiated by `TrayService.ShowMain` (`TrayService.cs:197-216`) when the user clicks the tray; receives `AppHost` via constructor and binds `SettingsViewModel` as `DataContext`.

There is no service registry beyond `AppHost`; everything hangs off the singleton instance held by `App.Host`.

## Concurrency model

- **SharpHook hook thread.** `TaskPoolGlobalHook` (`HotkeyService.cs:26`) marshals OS keyboard events onto a thread-pool task. `Pressed`/`Released` handlers on `DictationEngine` therefore run *off* the UI thread.
- **PortAudio audio thread.** `PortAudioCapture.OnAudio` (`PortAudioCapture.cs:143-158`) is invoked by the native audio callback. It uses `unsafe` pointer copy and `ConcurrentQueue<float[]>.Enqueue` for handoff — no managed locks on the hot path. `_isRecording` is `volatile`. The callback never touches the rest of the app.
- **Transcriber thread.** `Transcriber.EnsureLoaded` is guarded by `_lock` (`Transcriber.cs:14, 25-34`). `TranscribeAsync` is async and uses `Math.Max(2, Environment.ProcessorCount / 2)` whisper.cpp threads (`Transcriber.cs:56`).
- **DictationEngine.ProcessAsync.** Holds a `CancellationTokenSource` (`_cts`); a new release cancels the prior one (`DictationEngine.cs:98-100`). Errors are routed to the `Errored` event; `finally` always returns to `Idle`.
- **UI thread marshalling.** `TrayService.OnStateChanged` and `TrayService.OnConfigChanged` always `Dispatcher.UIThread.Post(...)` (`TrayService.cs:32, 173, 199`) — the engine never directly touches Avalonia controls. `SettingsViewModel` uses `CommunityToolkit.Mvvm` `[ObservableProperty]` + `[RelayCommand]`.
- **Hotkey capture (rebind).** `HotkeyService.CaptureNextKeyAsync` (`HotkeyService.cs:43-57`) uses a `TaskCompletionSource<KeyCode>` with `Task.Delay`-based timeout cancellation; non-modifier next-press resolves it.
- **Cancellation tokens** are plumbed through `IPasteService.PasteAsync`, `Transcriber.TranscribeAsync`, and `ModelDownloader.DownloadAsync` (`src/QuickSType.Core/Transcribe/ModelDownloader.cs`), which checks `cancellationToken.ThrowIfCancellationRequested()` per chunk.

## Configuration & persistence

- **Schema**: `AppConfig` record (`src/QuickSType.Core/Config/Config.cs`) with `Model`, `Languages`, `ActiveLanguage`, `Hotkey`, `AutoLanguage`, `StartAtLogin`, `SelectedAudioDevice`, `TranscriptionBackend` (auto/cpu/metal/cuda), `ShowNotifications`, `SchemaVersion=2`. Has fluent helpers `WithLanguage`, `WithAutoLanguage`, `WithEnabledLanguages` (lines 37-82) that normalize/dedupe and shift `ActiveLanguage` if the new list excludes it.
- **Storage**: `ConfigStore` (`src/QuickSType.Core/Config/ConfigStore.cs`).
  - `DefaultPath` (lines 25-40): `~/Library/Application Support/QuickSType/config.json` on Mac, `%APPDATA%\QuickSType\config.json` on Windows, `$XDG_CONFIG_HOME/quickstype/config.json` elsewhere.
  - `Save` is atomic-ish: writes `*.tmp`, then `File.Replace` or `File.Move` (lines 83-93).
  - `Load` (lines 44-81) deserializes via the AOT source-generated context, falls back to defaults on parse error, and triggers legacy migration if the new file is absent but `~/.config/quickstype/config.json` exists.
- **Source-gen JSON**: `ConfigJsonContext` in `src/QuickSType.Core/Config/JsonContext.cs:5-13` declares `[JsonSerializable(typeof(AppConfig))]` + `[JsonSerializable(typeof(LegacyPythonConfig))]` with `SnakeCaseLower` naming. Required for AOT.
- **Migration**: `MigrateFromPython` (`ConfigStore.cs:95-141`) maps the Python POC's `model` strings (`mlx-community/whisper-*` → `ggml-*`) and `hotkey` strings (`alt_r` → `VcRightAlt`, etc.). Migration is exhaustively pinned by `tests/QuickSType.Core.Tests/ConfigMigrationTests.cs` (theory tables for hotkey + model mappings, full-record migration, empty-defaults case).
- **Models**: not config — downloaded ggml `.bin` files. Catalog at `src/QuickSType.Core/Transcribe/ModelCatalog.cs` (default `ggml-large-v3-turbo-q5_0`, ~570 MB). Stored under `ModelCatalog.ModelsDirectory()` — `~/Library/Application Support/QuickSType/models` on Mac, `%LOCALAPPDATA%\QuickSType\models` on Win. `ModelDownloader.DownloadAsync` supports HTTP `Range` resume from a `*.bin.part` sidecar.

## Existing docs

`docs/ARCHITECTURE.md` already documents the Whisper.net pipeline, AOT decisions (`IsAotCompatible=true`, source-gen JSON, conditional native runtime packages), tray menu rebuild semantics, and known limitations (macOS Accessibility relaunch, AltGr collision, Windows low-level hook elevation gap, Metal-vs-MLX speed). This document is the implementation-side companion: it cites the actual classes that realize each design decision and walks the runtime call graph in source order.
