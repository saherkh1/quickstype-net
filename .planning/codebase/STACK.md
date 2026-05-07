---
type: codebase-map
focus: tech
last_mapped: 2026-05-07
---

# QuickSType.NET — Stack

QuickSType is a cross-platform (macOS + Windows) push-to-talk voice dictation desktop app written in C#/.NET 10 with an Avalonia UI shell, local Whisper transcription, and PortAudio capture. Distributed as a single AOT-compiled binary per platform.

## Languages

- **C#** — `<LangVersion>latest</LangVersion>` set globally in `Directory.Build.props` (line 5). Implicit usings + nullable enabled.
- **XAML (Avalonia)** — used for `App.axaml` and `Views/MainWindow.axaml` in the UI project.
- **PowerShell** — `build/publish-windows.ps1` build script.
- **Bash** — `build/publish-mac.sh` build script.
- **Python** (build-time only, not runtime) — `build/generate-icons.py` for tray-icon generation.

## Runtime / Platform

- **.NET SDK pinned** in `global.json`: `10.0.100`, `rollForward: latestFeature`, `allowPrerelease: false`.
- **Target framework** (single, applied to every project via `Directory.Build.props` line 4): `net10.0`.
- **Runtime identifiers** (chosen at publish time, not in csproj): `osx-arm64`, `osx-x64`, `win-x64` — see `build/publish-mac.sh` and `build/publish-windows.ps1`.
- **Minimum OS**:
  - macOS 13+ (`build/Info.plist` `LSMinimumSystemVersion = 13.0`).
  - Windows 10/11 x64 (`src/QuickSType.UI/app.manifest` declares `supportedOS` IDs covering Win 7..11).
- **Linux**: not officially targeted, but `Composition/AppHost.cs` (line 90+) installs `Noop*` services so the app runs (without paste/notify) under non-Mac/non-Windows hosts.
- **AOT-compatible**: every project sets `<IsAotCompatible>true</IsAotCompatible>` (`src/QuickSType.Core/QuickSType.Core.csproj` line 6, both Platform projects, and the UI project line 10).
- **Unsafe blocks** allowed in `Core`, `Platform.Mac`, `Platform.Windows` (used for the PortAudio float-pointer copy in `Audio/PortAudioCapture.cs:151`).

## Frameworks & Major Libraries

| Concern | Library | Version | Where |
|---|---|---|---|
| UI shell / windowing | Avalonia | 11.2.3 | `src/QuickSType.UI/QuickSType.UI.csproj` lines 14-18 |
| UI theme | Avalonia.Themes.Fluent | 11.2.3 | `src/QuickSType.UI/QuickSType.UI.csproj` line 16 |
| UI font | Avalonia.Fonts.Inter | 11.2.3 | `src/QuickSType.UI/QuickSType.UI.csproj` line 17 |
| MVVM helpers | CommunityToolkit.Mvvm | 8.4.2 | `src/QuickSType.UI/QuickSType.UI.csproj` line 19 |
| Logging abstractions | Microsoft.Extensions.Logging.Abstractions | 10.0.0 | `src/QuickSType.Core/QuickSType.Core.csproj` line 11 |
| Logging (concrete + console) | Microsoft.Extensions.Logging[.Console] | 10.0.0 | `src/QuickSType.UI/QuickSType.UI.csproj` lines 20-21 |
| Global keyboard hook | SharpHook (libuiohook) | 7.1.1 | `src/QuickSType.Core/QuickSType.Core.csproj` line 12 |
| Local speech-to-text | Whisper.net | 1.9.0 | `src/QuickSType.Core/QuickSType.Core.csproj` line 13 |
| Whisper.cpp native runtime | Whisper.net.Runtime | 1.9.0 | `src/QuickSType.UI/QuickSType.UI.csproj` line 22 |
| Whisper Mac CoreML accel | Whisper.net.Runtime.CoreML | 1.9.0 (Mac only, conditional) | `src/QuickSType.UI/QuickSType.UI.csproj` line 23 |
| Whisper Windows CUDA accel | Whisper.net.Runtime.Cuda.Windows | 1.9.0 (Win only, conditional) | `src/QuickSType.UI/QuickSType.UI.csproj` line 24 |
| Audio capture (managed wrapper) | PortAudioSharp2 | 1.0.6 | `src/QuickSType.Core/QuickSType.Core.csproj` line 14 |
| PortAudio native binaries (Mac arm64) | org.k2fsa.portaudio.runtime.osx-arm64 | 1.0.6 (Mac only) | `src/QuickSType.UI/QuickSType.UI.csproj` line 25 |
| PortAudio native binaries (Mac x64) | org.k2fsa.portaudio.runtime.osx-x64 | 1.0.6 (Mac only) | `src/QuickSType.UI/QuickSType.UI.csproj` line 26 |
| PortAudio native binaries (Win x64) | org.k2fsa.portaudio.runtime.win-x64 | 1.0.6 (Win only) | `src/QuickSType.UI/QuickSType.UI.csproj` line 27 |
| Clipboard | TextCopy | 6.2.1 | `src/QuickSType.Core/QuickSType.Core.csproj` line 15 |
| JSON | `System.Text.Json` (BCL) — source-gen via `ConfigJsonContext` | n/a (BCL with .NET 10) | `src/QuickSType.Core/Config/JsonContext.cs` |
| Test runner | xunit | 2.9.2 | `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj` line 11 |
| xunit VS integration | xunit.runner.visualstudio | 3.0.1 | `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj` line 12 |
| Test SDK | Microsoft.NET.Test.Sdk | 17.12.0 | `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj` line 10 |
| Assertions | Shouldly | 4.3.0 | `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj` line 13 |
| Avalonia Diagnostics (Debug only) | Avalonia.Diagnostics | 11.2.3 | `src/QuickSType.UI/QuickSType.UI.csproj` line 18 (Debug condition) |

No DI container is referenced; composition is hand-rolled in `src/QuickSType.UI/Composition/AppHost.cs` (singleton `AppHost.Create()` constructs and wires every service explicitly).

## Build & Tooling

- **Solution**: `QuickSType.sln` (5 projects: `Core`, `Platform.Mac`, `Platform.Windows`, `UI`, `Core.Tests`).
- **Common build properties**: `Directory.Build.props` (single file at repo root) sets `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `Deterministic=true`, suppresses `CA1416` and `NU1701`, and applies `Authors=Saher Khateeb`, `Product=QuickSType`, `Version=0.1.0`.
- **Release config**: portable PDBs + `Optimize=true` (`Directory.Build.props` lines 18-21).
- **Avalonia compiled bindings**: `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` in `src/QuickSType.UI/QuickSType.UI.csproj` line 9.
- **Application manifest**: `src/QuickSType.UI/app.manifest` declares `asInvoker` privileges and PerMonitorV2 DPI awareness.
- **AOT publish flags**:
  - macOS: `-p:PublishAot=true -p:StripSymbols=true -p:InvariantGlobalization=false`, plus a graceful fallback to self-contained-without-AOT if AOT publish fails (`build/publish-mac.sh` lines 17-31).
  - Windows: `-p:PublishAot=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`, with the same fallback (`build/publish-windows.ps1` lines 9-26).
- **Mac packaging**: `build/publish-mac.sh` wraps the publish output into `publish/QuickSType.app/Contents/{MacOS,Resources}`, copies `build/Info.plist`, and ad-hoc-signs (`codesign --force --deep --sign -`).
- **Windows packaging**: single-file `publish/win-x64/QuickSType.exe`.
- **Tray icon assets**: pre-rendered PNGs in `src/QuickSType.UI/Assets/tray-{idle,recording,processing}{,@2x,@3x}.png`, included via `<AvaloniaResource Include="Assets\**" />`. Generator: `build/generate-icons.py`.
- **CI**: no `.github/workflows/` directory present in the repo as of mapping.

## Configuration

- **Schema**: `AppConfig` record in `src/QuickSType.Core/Config/Config.cs` — fields `model`, `languages` (list), `active_language`, `hotkey`, `auto_language`, `start_at_login`, `selected_audio_device`, `transcription_backend` (`auto`/`cpu`), `show_notifications`, `schema_version` (currently `2`).
- **Defaults**: `Model=ggml-base`, `Hotkey=VcRightAlt`, `Languages=["en"]`, `ShowNotifications=true`. Note: the runtime *recommended* model in `ModelCatalog.Default` is `ggml-large-v3-turbo-q5_0`, but `AppConfig.Model` defaults to `ggml-base` until the first-run wizard / settings UI changes it.
- **Storage** (chosen by `ConfigStore.DefaultPath()` at `src/QuickSType.Core/Config/ConfigStore.cs:25`):
  - macOS: `~/Library/Application Support/QuickSType/config.json`
  - Windows: `%APPDATA%\QuickSType\config.json`
  - Linux fallback: `$XDG_CONFIG_HOME/quickstype/config.json` or `~/.config/quickstype/config.json`
- **Atomic save**: `ConfigStore.Save` writes to `config.json.tmp` and uses `File.Replace`/`File.Move` (`ConfigStore.cs:83-93`).
- **Legacy migration**: if the new path is empty but `~/.config/quickstype/config.json` exists, `ConfigStore.Load` deserializes the legacy Python schema (`LegacyPythonConfig` in `Config/JsonContext.cs`) and rewrites it via `MigrateFromPython` — translating `mlx-community/whisper-*` model names to `ggml-*` IDs and Python hotkey names (`alt_r`, `cmd_l`, `f5`, …) to SharpHook `KeyCode` names (`VcRightAlt`, `VcLeftMeta`, `VcF5`, …).
- **JSON serializer**: AOT-safe source-gen via `[JsonSourceGenerationOptions(WriteIndented=true, PropertyNamingPolicy=JsonKnownNamingPolicy.SnakeCaseLower)]` in `src/QuickSType.Core/Config/JsonContext.cs`.
- **Environment variables read**: `HOME` (Mac/Linux), `XDG_CONFIG_HOME`, `XDG_DATA_HOME` — used only by `ConfigStore.DefaultPath()` and `ModelCatalog.ModelsDirectory()`. No `appsettings.json` and no `dotenv`-style config.
- **No secrets** stored anywhere; the app is fully offline.

## Dependency Inventory

| PackageId | Version | Used by project |
|---|---|---|
| Microsoft.Extensions.Logging.Abstractions | 10.0.0 | `src/QuickSType.Core/QuickSType.Core.csproj` |
| SharpHook | 7.1.1 | `src/QuickSType.Core/QuickSType.Core.csproj` |
| Whisper.net | 1.9.0 | `src/QuickSType.Core/QuickSType.Core.csproj` |
| PortAudioSharp2 | 1.0.6 | `src/QuickSType.Core/QuickSType.Core.csproj` |
| TextCopy | 6.2.1 | `src/QuickSType.Core/QuickSType.Core.csproj` |
| Avalonia | 11.2.3 | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Avalonia.Desktop | 11.2.3 | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Avalonia.Themes.Fluent | 11.2.3 | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Avalonia.Fonts.Inter | 11.2.3 | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Avalonia.Diagnostics | 11.2.3 (Debug only) | `src/QuickSType.UI/QuickSType.UI.csproj` |
| CommunityToolkit.Mvvm | 8.4.2 | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Microsoft.Extensions.Logging | 10.0.0 | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Microsoft.Extensions.Logging.Console | 10.0.0 | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Whisper.net.Runtime | 1.9.0 | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Whisper.net.Runtime.CoreML | 1.9.0 (Mac, conditional) | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Whisper.net.Runtime.Cuda.Windows | 1.9.0 (Win, conditional) | `src/QuickSType.UI/QuickSType.UI.csproj` |
| org.k2fsa.portaudio.runtime.osx-arm64 | 1.0.6 (Mac, conditional) | `src/QuickSType.UI/QuickSType.UI.csproj` |
| org.k2fsa.portaudio.runtime.osx-x64 | 1.0.6 (Mac, conditional) | `src/QuickSType.UI/QuickSType.UI.csproj` |
| org.k2fsa.portaudio.runtime.win-x64 | 1.0.6 (Win, conditional) | `src/QuickSType.UI/QuickSType.UI.csproj` |
| Microsoft.NET.Test.Sdk | 17.12.0 | `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj` |
| xunit | 2.9.2 | `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj` |
| xunit.runner.visualstudio | 3.0.1 | `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj` |
| Shouldly | 4.3.0 | `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj` |

`src/QuickSType.Platform.Mac/QuickSType.Platform.Mac.csproj` and `src/QuickSType.Platform.Windows/QuickSType.Platform.Windows.csproj` carry no NuGet packages — they only `<ProjectReference>` Core and rely on platform P/Invoke (see INTEGRATIONS.md).
