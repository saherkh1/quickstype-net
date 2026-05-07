---
type: codebase-map
focus: arch
last_mapped: 2026-05-07
---

# QuickSType.NET — Repo Structure

## Repo tree

```
quickstype-net/
├── QuickSType.sln                 5-project solution (Core, Platform.Mac, Platform.Windows, UI, Core.Tests)
├── Directory.Build.props          shared MSBuild props: net10.0, nullable, AOT-friendly defaults, Version=0.1.0
├── global.json                    pins .NET SDK 10.0.100 (latestFeature)
├── README.md                      build/run quickstart
├── .gitignore                     ignores bin/obj/publish/*.app/*.dmg/*.bin/models/...
├── src/
│   ├── QuickSType.Core/           pure cross-platform domain (no platform refs)
│   │   ├── QuickSType.Core.csproj — IsAotCompatible, refs SharpHook + Whisper.net + PortAudioSharp2 + TextCopy
│   │   ├── Audio/                 mic capture port + PortAudio adapter + WAV writer
│   │   ├── Config/                AppConfig record, ConfigStore, source-gen JSON context
│   │   ├── Hotkey/                SharpHook wrapper + key parsing/capture
│   │   ├── Paste/                 IPasteService port (Core defines, platform projects implement)
│   │   ├── Platform/              port interfaces: IPermissionService, IAutoLaunchService, INotificationService
│   │   ├── Transcribe/            Transcriber (Whisper.net) + ModelCatalog + ModelDownloader
│   │   ├── DictationEngine.cs     orchestrates hotkey → audio → transcribe → paste; owns DictationState
│   │   └── Languages.cs           static list of 16 supported language codes/names
│   ├── QuickSType.Platform.Mac/   Mac adapters via P/Invoke + osascript/launchctl shell-out
│   │   ├── MacPasteService.cs     — CGEvent Cmd+V + clipboard preserve/restore
│   │   ├── MacPermissions.cs      — AXIsProcessTrusted, x-apple.systempreferences URIs
│   │   ├── MacAutoLaunch.cs       — LaunchAgent plist + launchctl
│   │   └── MacNotifications.cs    — osascript notification + afplay
│   ├── QuickSType.Platform.Windows/  Windows adapters via P/Invoke + Registry + PowerShell
│   │   ├── WindowsPasteService.cs   — user32.SendInput Ctrl+V
│   │   ├── WindowsPermissions.cs    — ms-settings: URIs (no-op checks)
│   │   ├── WindowsAutoLaunch.cs     — HKCU\...\Run registry value
│   │   └── WindowsNotifications.cs  — PowerShell NotifyIcon + kernel32.Beep
│   └── QuickSType.UI/             Avalonia 11.2 app — entry point + composition root + tray + settings
│       ├── Program.cs             [STAThread] Main → AppBuilder → ClassicDesktopLifetime
│       ├── App.axaml(.cs)         OnFrameworkInitializationCompleted: AppHost.Create + TrayService.Install
│       ├── app.manifest           Win32 manifest
│       ├── Composition/           AppHost.cs — single composition root, picks adapters by RuntimeInformation
│       ├── Tray/                  TrayService.cs — TrayIcon + NativeMenu (state, languages submenu, settings, quit)
│       ├── Views/                 MainWindow.axaml(.cs) — tabbed settings window (General/Languages/Models/About)
│       ├── ViewModels/            SettingsViewModel.cs — CommunityToolkit.Mvvm bindings
│       └── Assets/                tray-idle/recording/processing icons (Avalonia resources)
├── tests/
│   └── QuickSType.Core.Tests/     xunit + Shouldly; references Core only
│       ├── ConfigMigrationTests.cs   — Python→.NET hotkey/model maps + full migration
│       ├── ConfigRoundTripTests.cs   — JSON serialize/deserialize parity
│       ├── HotkeyParsingTests.cs     — KeyCode parsing + fallback
│       ├── LanguageValidationTests.cs — language code lookup + WithEnabledLanguages
│       ├── ModelCatalogTests.cs      — catalog lookups, paths
│       ├── WavWriterTests.cs         — PCM16 header/data correctness
│       └── GlobalUsings.cs           — global xunit + Shouldly usings
├── docs/
│   └── ARCHITECTURE.md            handwritten design overview (companion to .planning/codebase/ARCHITECTURE.md)
├── build/                         publish + packaging scripts (not MSBuild targets)
│   ├── publish-mac.sh             dotnet publish (osx-arm64/osx-x64) → wrap into QuickSType.app
│   ├── publish-windows.ps1        dotnet publish win-x64 AOT
│   ├── Info.plist                 Mac bundle metadata (LSUIElement, NSMicrophoneUsageDescription, ...)
│   └── generate-icons.py          generates tray PNGs / .icns / .ico
└── .planning/
    └── codebase/                  this folder — codebase maps (ARCHITECTURE.md, STRUCTURE.md)
```

## Project layout conventions

### Inside `src/QuickSType.Core`

| Folder | Role |
|---|---|
| `Audio/` | Mic-capture port (`IAudioCapture.cs`) + PortAudio adapter (`PortAudioCapture.cs`) + `WavWriter` (PCM16). Adapter lives here, not in `Platform.*`, because PortAudio is itself cross-platform via runtime NuGet packages. |
| `Config/` | `Config.cs` (the `AppConfig` record + fluent helpers), `ConfigStore.cs` (file I/O + legacy migration), `JsonContext.cs` (source-generated JSON for AOT). |
| `Hotkey/` | `HotkeyService.cs` — single class wrapping `SharpHook.TaskPoolGlobalHook` and exposing `Pressed`/`Released` events + `CaptureNextKeyAsync` for rebind UX. |
| `Paste/` | Just the `IPasteService` port. Implementations live in the platform projects. (Note: this lives at the top level, not inside `Platform/`, because `Paste` is the most user-visible action — historical convention from the docs.) |
| `Platform/` | The remaining ports: `IPermissionService`, `IAutoLaunchService`, `INotificationService`. |
| `Transcribe/` | `Transcriber.cs` (Whisper.net wrapper, lazy load + lock), `ModelCatalog.cs` (static list of ggml models + path resolution), `ModelDownloader.cs` (HTTP Range-resume download). |
| `DictationEngine.cs`, `Languages.cs` | Top-level — central orchestrator and shared static data. |

### Inside `src/QuickSType.UI`

| Folder | Role |
|---|---|
| `Composition/` | `AppHost.cs` — composition root + `Noop*` fallback adapters. The only place `RuntimeInformation.IsOSPlatform` is consulted. |
| `Tray/` | `TrayService.cs` — Avalonia `TrayIcon` + `NativeMenu`, owns the live language submenu and binds engine `StateChanged` to icon swap. |
| `Views/` | `App.axaml`, `MainWindow.axaml(.cs)` — Avalonia XAML + code-behind. `MainTab` enum lives here. |
| `ViewModels/` | `SettingsViewModel.cs` — `CommunityToolkit.Mvvm` `[ObservableProperty]` + `[RelayCommand]` for the settings window. |
| `Assets/` | Avalonia resources (tray icons under `avares://QuickSType/Assets/...`). Loaded via `AssetLoader.Open`. |

## Naming conventions

Derived from observed files (no separate style doc):

- **Ports / interfaces**: `I<Name>Service` — `IPasteService`, `IPermissionService`, `IAutoLaunchService`, `INotificationService`, `IAudioCapture` (the audio port deviates: `IAudioCapture`, no "Service" suffix, because it's not a passive service — it has lifecycle state).
- **Platform implementations**: `<Os><Name>` — `MacPasteService` / `WindowsPasteService`, `MacPermissions` / `WindowsPermissions`, `MacAutoLaunch` / `WindowsAutoLaunch`, `MacNotifications` / `WindowsNotifications`. Class names mirror the port stem; namespace is `QuickSType.Platform.Mac` / `QuickSType.Platform.Windows`.
- **Tests**: `<TypeUnderTest>Tests` — `ConfigMigrationTests`, `ConfigRoundTripTests`, `HotkeyParsingTests`, `LanguageValidationTests`, `ModelCatalogTests`, `WavWriterTests`. xunit `[Fact]` + `[Theory]` with `Shouldly` `.ShouldBe(...)` style.
- **Records vs classes**: immutable data uses `sealed record` (`AppConfig`, `LegacyPythonConfig`, `LanguageInfo`, `ModelInfo`, `AudioDeviceInfo`); services and stateful objects are `sealed class` with private fields and `IDisposable` where they own native resources.
- **P/Invoke**: source-generated where possible — classes with native imports are declared `partial` and use `[LibraryImport(...)]` (e.g. `MacPasteService`, `MacPermissions`, `WindowsPasteService`, `WindowsNotifications`). Falls back to `[DllImport]` only inside the nested `CFStringHandle` helper in `MacPermissions.cs`.
- **Hotkey codes**: SharpHook `KeyCode.Vc<Name>` is the canonical wire format (`VcRightAlt`, `VcLeftMeta`, `VcF13`); legacy Python strings (`alt_r`, `cmd`, `f5`) only appear in `MapPythonHotkey` for migration.
- **Model IDs**: `ggml-<size>[-<variant>]` — matches whisper.cpp HuggingFace filenames (`ggml-base`, `ggml-large-v3-turbo-q5_0`).

## Key locations cheatsheet

| Looking for… | File |
|---|---|
| Process entry point | `src/QuickSType.UI/Program.cs` |
| Composition root + adapter selection | `src/QuickSType.UI/Composition/AppHost.cs` |
| Avalonia startup sequence | `src/QuickSType.UI/App.axaml.cs` |
| Hotkey handling | `src/QuickSType.Core/Hotkey/HotkeyService.cs` |
| End-to-end pipeline orchestration | `src/QuickSType.Core/DictationEngine.cs` |
| Mic capture | `src/QuickSType.Core/Audio/PortAudioCapture.cs` (port: `Audio/IAudioCapture.cs`) |
| WAV encoding | `src/QuickSType.Core/Audio/WavWriter.cs` |
| Whisper transcription | `src/QuickSType.Core/Transcribe/Transcriber.cs` |
| Model list + paths | `src/QuickSType.Core/Transcribe/ModelCatalog.cs` |
| Model download (resumable) | `src/QuickSType.Core/Transcribe/ModelDownloader.cs` |
| Paste impl — Mac | `src/QuickSType.Platform.Mac/MacPasteService.cs` |
| Paste impl — Windows | `src/QuickSType.Platform.Windows/WindowsPasteService.cs` |
| Permissions — Mac | `src/QuickSType.Platform.Mac/MacPermissions.cs` |
| Auto-launch — Mac plist | `src/QuickSType.Platform.Mac/MacAutoLaunch.cs` |
| Auto-launch — Windows registry | `src/QuickSType.Platform.Windows/WindowsAutoLaunch.cs` |
| Config schema | `src/QuickSType.Core/Config/Config.cs` |
| Config save/load + legacy migration | `src/QuickSType.Core/Config/ConfigStore.cs` |
| AOT JSON source-gen | `src/QuickSType.Core/Config/JsonContext.cs` |
| Tray menu | `src/QuickSType.UI/Tray/TrayService.cs` |
| Settings window | `src/QuickSType.UI/Views/MainWindow.axaml(.cs)` |
| Settings VM | `src/QuickSType.UI/ViewModels/SettingsViewModel.cs` |
| Supported languages | `src/QuickSType.Core/Languages.cs` |
| Tests | `tests/QuickSType.Core.Tests/` |
| Shared MSBuild props (TFM, nullable, AOT) | `Directory.Build.props` |
| SDK pin | `global.json` |
| Solution | `QuickSType.sln` |
| Mac packaging | `build/publish-mac.sh`, `build/Info.plist` |
| Windows packaging | `build/publish-windows.ps1` |
| Tray icons / .icns / .ico generator | `build/generate-icons.py` |
| Existing design doc | `docs/ARCHITECTURE.md` |

## Generated / vendored / ignored

`.gitignore` ignores:

- **Build outputs** — `bin/`, `obj/`, `out/`, `publish/`, `*.app/`, `*.dmg`, `*.pkg`, `*.exe`, `*.msi`, `*.zip`, `artifacts/`. Each project gets its own `bin/` and `obj/` next to its `.csproj`; `src/QuickSType.UI/bin/` and `obj/` are visible on disk but not in git.
- **NuGet** — `*.nupkg`, `*.snupkg`, `.nuget/`, `packages/`, `project.lock.json`, `project.fragment.lock.json`.
- **IDE** — `.vs/`, `.vscode/`, `.idea/`, `*.user`, `*.suo`, `*.userprefs`.
- **Logs** — `*.log`, `logs/`.
- **Local config / secrets** — `*.local.json`, `*.local`, `appsettings.Development.json`.
- **Models** — `models/`, `*.bin`, `*.mlmodelc/`. Whisper ggml weights are runtime-downloaded into `~/Library/Application Support/QuickSType/models` (Mac) or `%LOCALAPPDATA%\QuickSType\models` (Win) and are never committed.

Nothing is vendored under the repo. Native dependencies are pulled in as conditional NuGet packages from `src/QuickSType.UI/QuickSType.UI.csproj:22-27` (`Whisper.net.Runtime[.CoreML|.Cuda.Windows]`, `org.k2fsa.portaudio.runtime.{osx-arm64|osx-x64|win-x64}`) and unpacked into the publish output by AOT.

The `build/` directory is intentionally **not** wired into MSBuild — its scripts are invoked manually (or by CI) after `dotnet publish`. `generate-icons.py` produces the PNGs that get committed under `src/QuickSType.UI/Assets/` and consumed via `AvaloniaResource` (see `QuickSType.UI.csproj:36-38`).
