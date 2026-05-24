<!-- GSD:project-start source:PROJECT.md -->
## Project

**QuickSType**

QuickSType is a cross-platform (macOS + Windows) push-to-talk voice dictation desktop app. Hold a hotkey, speak, release — the transcribed text appears at your cursor. 100% local transcription via whisper.cpp through Whisper.net (Metal on Mac, CUDA on Windows). Distributed as a single AOT-compiled binary per platform. This milestone takes the existing .NET codebase from working prototype to a polished, open-source v1 release.

**Core Value:** Streaming, live-as-you-speak dictation that feels effortless and stays 100% local — words appear in the focused text field within ~500ms of being spoken, no cloud, no account, no friction.

### Constraints

- **Tech stack** — C# / .NET 10 / Avalonia 11.2 / whisper.cpp via Whisper.net. No UI framework rewrite. — Existing investment; team velocity.
- **AOT compatibility** — Every project sets `<IsAotCompatible>true</IsAotCompatible>`. New libraries must not break AOT (no reflection-based serialization, etc.). — Required for single-binary distribution and fast startup.
- **Performance — streaming latency** — <500ms perceived word-to-screen latency on Apple Silicon and CUDA-equipped Windows. Older Intel Macs and low-RAM Windows boxes auto-fall-back to commit-on-pause. — User-stated quality bar.
- **Privacy / network surface** — 100% local transcription. The only outbound network calls are: (a) signed Whisper model downloads from Hugging Face, (b) auto-update version checks, (c) opt-in crash reports. — Hard product principle.
- **Binary size** — Target ~40–60 MB per platform per the README. — Distribution friction; download/install UX.
- **Platforms** — macOS 13+ (Apple Silicon + Intel) and Windows 10/11 x64. Linux remains best-effort via Noop services, not a release target. — Stated in README; matches existing build scripts.
- **License** — MIT (per current README). Will ship as MIT in the public repo. — Already documented.
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->
## Technology Stack

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
## Build & Tooling
- **Solution**: `QuickSType.sln` (5 projects: `Core`, `Platform.Mac`, `Platform.Windows`, `UI`, `Core.Tests`).
- **Common build properties**: `Directory.Build.props` (single file at repo root) sets `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `Deterministic=true`, suppresses `CA1416` and `NU1701`, and applies `Authors=Saher Khateeb`, `Product=QuickSType`, `Version=0.1.0`.
- **Release config**: portable PDBs + `Optimize=true` (`Directory.Build.props` lines 18-21).
- **Avalonia compiled bindings**: `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` in `src/QuickSType.UI/QuickSType.UI.csproj` line 9.
- **Application manifest**: `src/QuickSType.UI/app.manifest` declares `asInvoker` privileges and PerMonitorV2 DPI awareness.
- **AOT publish flags**:
- **Mac packaging**: `build/publish-mac.sh` wraps the publish output into `publish/QuickSType.app/Contents/{MacOS,Resources}`, copies `build/Info.plist`, and ad-hoc-signs (`codesign --force --deep --sign -`).
- **Windows packaging**: single-file `publish/win-x64/QuickSType.exe`.
- **Tray icon assets**: pre-rendered PNGs in `src/QuickSType.UI/Assets/tray-{idle,recording,processing}{,@2x,@3x}.png`, included via `<AvaloniaResource Include="Assets\**" />`. Generator: `build/generate-icons.py`.
- **CI**: no `.github/workflows/` directory present in the repo as of mapping.
## Configuration
- **Schema**: `AppConfig` record in `src/QuickSType.Core/Config/Config.cs` — fields `model`, `languages` (list), `active_language`, `hotkey`, `auto_language`, `start_at_login`, `selected_audio_device`, `transcription_backend` (`auto`/`cpu`), `show_notifications`, `schema_version` (currently `2`).
- **Defaults**: `Model=ggml-base`, `Hotkey=VcRightAlt`, `Languages=["en"]`, `ShowNotifications=true`. Note: the runtime *recommended* model in `ModelCatalog.Default` is `ggml-large-v3-turbo-q5_0`, but `AppConfig.Model` defaults to `ggml-base` until the first-run wizard / settings UI changes it.
- **Storage** (chosen by `ConfigStore.DefaultPath()` at `src/QuickSType.Core/Config/ConfigStore.cs:25`):
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
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

## Language features in use
- **File-scoped namespaces** — used uniformly. Examples: `namespace QuickSType.Core;` in `src/QuickSType.Core/Languages.cs:1`, `namespace QuickSType.Core.Config;` in `src/QuickSType.Core/Config/Config.cs:3`, `namespace QuickSType.Core.Hotkey;` in `src/QuickSType.Core/Hotkey/HotkeyService.cs:6`.
- **Nullable reference types** — enabled project-wide (`<Nullable>enable</Nullable>`). Nullable annotations are used pervasively, e.g. `ILogger<HotkeyService>? log = null` in `src/QuickSType.Core/Hotkey/HotkeyService.cs:22`, `string? SelectedAudioDevice { get; init; }` in `src/QuickSType.Core/Config/Config.cs:26`, `WhisperFactory? _factory` in `src/QuickSType.Core/Transcribe/Transcriber.cs:12`.
- **Records** — used for immutable value types and DTOs. `public sealed record AppConfig` (`src/QuickSType.Core/Config/Config.cs:5`), `public sealed record LegacyPythonConfig` (`src/QuickSType.Core/Config/JsonContext.cs:15`), `public sealed record LanguageInfo(string Code, string DisplayName, string NativeName)` (`src/QuickSType.Core/Languages.cs:5`), `public sealed record ModelInfo(...)` (`src/QuickSType.Core/Transcribe/ModelCatalog.cs:3`), and a `readonly record struct AudioDeviceInfo(...)` in `src/QuickSType.Core/Audio/IAudioCapture.cs:16`.
- **`with` expressions** — the `AppConfig` mutators use record `with` heavily, e.g. `return this with { ActiveLanguage = normalised, Languages = ..., AutoLanguage = false };` in `src/QuickSType.Core/Config/Config.cs:42`.
- **Collection expressions / spread** — `[.. Languages, normalised]` in `src/QuickSType.Core/Config/Config.cs:45`, `["en"]` literal in `src/QuickSType.Core/Config/ConfigStore.cs:100`, `return [];` in `src/QuickSType.Core/Audio/PortAudioCapture.cs:104`.
- **Target-typed `new`** — `private readonly object _lock = new();` (`src/QuickSType.Core/Transcribe/Transcriber.cs:14`), `private readonly ConcurrentQueue<float[]> _chunks = new();` (`src/QuickSType.Core/Audio/PortAudioCapture.cs:15`), `_hook = new TaskPoolGlobalHook();` is normal-typed for clarity at `src/QuickSType.Core/Hotkey/HotkeyService.cs:26`.
- **Pattern matching / `switch` expressions** — `MapPythonHotkey` and `MapPythonModel` are expression-bodied switches in `src/QuickSType.Core/Config/ConfigStore.cs:108-141`, including `var s when s.StartsWith("Vc") => s` patterns. `IsModifier` uses `or`-pattern in `src/QuickSType.Core/Hotkey/HotkeyService.cs:86-93`. Property-pattern `legacy.Languages is { Count: > 0 }` at `src/QuickSType.Core/Config/ConfigStore.cs:100`.
- **`is { } info` declaration patterns** — `Find(code) is { } info ? info.DisplayName : code.ToUpperInvariant()` in `src/QuickSType.Core/Languages.cs:31`.
- **Implicit usings** — `<ImplicitUsings>enable</ImplicitUsings>` is on; files only `using` non-default namespaces.
- **Top-level statements** — *not* used. `Program.Main` is an explicit `[STAThread] static int Main(string[] args)` in `src/QuickSType.UI/Program.cs:7-19`.
- **Primary constructors** — *not* used on classes. Constructors are written out long-form (e.g. `DictationEngine`, `HotkeyService`, `ConfigStore`). Primary-constructor-style is reserved for record positional parameters (`LanguageInfo`, `ModelInfo`).
- **`async` / `await using`** — `await using var wav = new MemoryStream(...);` (`src/QuickSType.Core/Transcribe/Transcriber.cs:52`), `await foreach (var segment in processor.ProcessAsync(...))` (line 68).
- **`unsafe` blocks** — guarded by `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in `src/QuickSType.Core/QuickSType.Core.csproj:7`. Used in the audio callback at `src/QuickSType.Core/Audio/PortAudioCapture.cs:151-155`.
## Compiler & lint settings
- `<TargetFramework>net10.0</TargetFramework>`
- `<LangVersion>latest</LangVersion>`
- `<Nullable>enable</Nullable>`
- `<ImplicitUsings>enable</ImplicitUsings>`
- `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>` — warnings do **not** fail the build.
- `<NoWarn>$(NoWarn);CA1416;NU1701</NoWarn>` — silences platform-compatibility (`CA1416`) and package-target-mismatch (`NU1701`) warnings, which are expected because Mac/Windows platform projects reference cross-OS APIs.
- `<GenerateDocumentationFile>false</GenerateDocumentationFile>`
- `<Deterministic>true</Deterministic>`
- Release config: `<DebugType>portable</DebugType>` + `<Optimize>true</Optimize>`.
- **No `<AnalysisLevel>` set** — defaults to the SDK's bundled level for `net10.0`.
- **No `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`** and **no `<EnforceCodeStyleInBuild>`**, so style violations don't break CI.
## Naming & style
- **PascalCase** for types, public members, methods, and constants.
- **camelCase** for parameters and locals (`samples`, `wavBytes`, `modelPath`, `useGpu`).
- **`_camelCase`** for private instance fields (`_log`, `_audio`, `_transcriber`, `_paste`, `_notify`, `_configStore`, `_config`, `_state`, `_cts`).
- **`I*` interfaces** — `IAudioCapture`, `IPasteService`, `INotificationService`, `IPermissionService`, `IAutoLaunchService`.
- **`s_` / `t_`** prefixes are *not* used; static fields (e.g. `InitLock`, `_initialized`) follow PascalCase or `_camelCase` mixed with `private static`.
- **`sealed` by default** — most concrete classes are `sealed` (`DictationEngine`, `HotkeyService`, `Transcriber`, `PortAudioCapture`, `ModelDownloader`, `AppHost`). `ConfigStore` is the notable non-sealed type, deliberately so that `ConfigStoreWithExists` can subclass it (`src/QuickSType.UI/Composition/AppHost.cs:132`).
## Error handling pattern
- **Long-lived services** (`DictationEngine`, `HotkeyService`) wrap user-callable entry points in `try / catch (Exception ex)`, log via `ILogger.LogError`, then either swallow (for handler invocations) or re-emit through a `public event Action<Exception>? Errored`.
- **Pipeline tasks** use a top-level `try / catch / finally` to guarantee state reset:
- **Event-handler invocations are wrapped defensively** so a misbehaving subscriber cannot crash the engine: `try { StateChanged?.Invoke(s); } catch (Exception ex) { _log.LogError(ex, "StateChanged handler threw"); }` at `src/QuickSType.Core/DictationEngine.cs:157-158`, mirrored in `HotkeyService.cs:71-72,81-82`.
- **`Dispose` swallows** to avoid throwing in finalisers/cleanup: `catch { /* swallow on dispose */ }` (`HotkeyService.cs:111`, `PortAudioCapture.cs:166`, `AppHost.cs:121`).
- **Invariant-violation throws** use stock BCL exception types — no custom exception hierarchy exists. Examples: `throw new InvalidOperationException("Model not loaded. Call EnsureLoaded() first.")` at `src/QuickSType.Core/Transcribe/Transcriber.cs:43`, and `Errored?.Invoke(new FileNotFoundException("Whisper model not installed.", modelPath))` at `src/QuickSType.Core/DictationEngine.cs:118`.
- **Recoverable load failures** log a warning and fall back: `ConfigStore.Load()` at `src/QuickSType.Core/Config/ConfigStore.cs:48-58` catches a corrupt config and returns a fresh `AppConfig`.
## Logging
- Every service that takes a logger accepts an **optional** `ILogger<T>?` and falls back to `NullLogger.Instance`, so Core types can be unit-tested or instantiated without a logger factory:
- The composition root (`AppHost.Create`) builds a single `ILoggerFactory` with `AddConsole` at `LogLevel.Information` (`src/QuickSType.UI/Composition/AppHost.cs:58-62`) and hands `CreateLogger<T>()` to each service.
- **`Console.WriteLine` is reserved for fatal-path output only** — used once in `Program.Main` for an unhandled top-level exception (`src/QuickSType.UI/Program.cs:16`).
- Log calls use **structured-message templates with named placeholders**, never string interpolation: `_log.LogInformation("Loaded Whisper model from {Path}", modelPath)`, `_log.LogDebug("Press ignored; state is {State}", _state)`, `_log.LogWarning(ex, "Error stopping PortAudio stream")`. This keeps the call sites AOT/source-generator friendly.
## Async / cancellation
- All I/O-bound work is `async Task` / `Task<T>`. Examples: `Transcriber.TranscribeAsync(ReadOnlyMemory<float> samples, int sampleRate, AppConfig config, CancellationToken cancellationToken = default)` (`src/QuickSType.Core/Transcribe/Transcriber.cs:36-41`), `ModelDownloader.DownloadAsync(ModelInfo model, IProgress<Progress>? progress = null, CancellationToken cancellationToken = default)` (`src/QuickSType.Core/Transcribe/ModelDownloader.cs:25-29`), `IPasteService.PasteAsync(string text, CancellationToken cancellationToken = default)` (`src/QuickSType.Core/Paste/IPasteService.cs:5`).
- **`CancellationToken` propagation is consistent** down through the stack — `DictationEngine` owns a `CancellationTokenSource _cts` (`DictationEngine.cs:29`), cancels-and-replaces on each release (`DictationEngine.cs:98-100`), and forwards the token into `Transcriber.TranscribeAsync` and `IPasteService.PasteAsync`. `ModelDownloader` cooperatively checks `cancellationToken.ThrowIfCancellationRequested()` inside its read loop (`ModelDownloader.cs:73`).
- **Fire-and-forget is explicit**: discarded with `_ =` rather than ignored, e.g. `_ = _notify.PlayStartAsync();` (`DictationEngine.cs:77`), `_ = ProcessAsync(samples, _cts.Token);` (`DictationEngine.cs:100`), `_ = Hotkey.RunAsync();` (`AppHost.cs:111`).
- **No `ConfigureAwait(false)`** — the project relies on the .NET 8+ default `ConfigureAwait` semantics for library code; this is acceptable here because the desktop app uses Avalonia's UI dispatcher and there is no synchronisation-context mismatch hot-path. *Worth noting* if the library is ever consumed in an ASP.NET Core context.
- **No `async void`** outside of (implicit) UI event handlers in the Avalonia layer.
## DI / composition
## JSON / serialization
- **`System.Text.Json`** with the **source-generator** is the only JSON path. The single context lives at `src/QuickSType.Core/Config/JsonContext.cs`:
- Snake-case property names are produced by the generator; per-property `[JsonPropertyName]` attributes are also set on every field on `AppConfig` (e.g. `[JsonPropertyName("active_language")]` at `src/QuickSType.Core/Config/Config.cs:13`) — this is belt-and-braces but useful as documentation.
- `ConfigStore.Load` / `Save` go through `ConfigJsonContext.Default.AppConfig` (and `.LegacyPythonConfig` for the migration path) — never `JsonSerializer.Serialize<T>(...)` with a runtime-reflection overload. This is what keeps the Core library AOT-compatible (`<IsAotCompatible>true</IsAotCompatible>` in `QuickSType.Core.csproj`).
- **Atomic save**: `ConfigStore.Save` writes `<path>.tmp` first, then `File.Replace`/`File.Move` — see `src/QuickSType.Core/Config/ConfigStore.cs:88-92`. This avoids leaving a half-written config on crash.
- **AOT considerations**: no reflection-based serialization, no expression trees in `JsonSerializer` calls, no `JsonElement` parsing. Adding a new persisted type means adding a `[JsonSerializable(typeof(T))]` line to `ConfigJsonContext` and routing through `ConfigJsonContext.Default.T` — the generator handles trim/AOT safety. New polymorphic shapes would need explicit `[JsonDerivedType]` attributes.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

## High-level pattern
- A **pure core** (`QuickSType.Core`) that owns domain logic and defines OS-shaped *ports* as C# interfaces.
- Two **platform adapters** (`QuickSType.Platform.Mac`, `QuickSType.Platform.Windows`) that implement those ports via P/Invoke or shell-out.
- A **single composition root + UI host** (`QuickSType.UI`, an Avalonia desktop app) that picks the correct adapter at runtime and wires everything into the dictation pipeline.
- A **test ring** (`QuickSType.Core.Tests`) targeting only the core.
## Project / Layer breakdown
| Project | Role | Depends on |
|---|---|---|
| `QuickSType.Core` (`src/QuickSType.Core/QuickSType.Core.csproj`) | Pure cross-platform domain: hotkey wrapper, audio capture, Whisper transcription, model catalog/downloader, config schema + persistence, platform port interfaces. AOT-compatible. | NuGet only — `SharpHook`, `Whisper.net`, `PortAudioSharp2`, `TextCopy`, `Microsoft.Extensions.Logging.Abstractions`. No project refs. |
| `QuickSType.Platform.Mac` (`src/QuickSType.Platform.Mac/QuickSType.Platform.Mac.csproj`) | Mac adapters via P/Invoke (`CoreGraphics`, `ApplicationServices`, `CoreFoundation`) and shell-out to `osascript`/`afplay`/`launchctl`/`open`. | `QuickSType.Core` only. |
| `QuickSType.Platform.Windows` (`src/QuickSType.Platform.Windows/QuickSType.Platform.Windows.csproj`) | Windows adapters: `user32.SendInput` for paste, `kernel32.Beep` for sounds, `Microsoft.Win32.Registry` for autostart, `ms-settings:` URIs. | `QuickSType.Core` only. |
| `QuickSType.UI` (`src/QuickSType.UI/QuickSType.UI.csproj`) | Avalonia 11.2 desktop app. Composition root, tray, settings window, view models. Output `Exe` named `QuickSType`. Runtime native packages (Whisper, PortAudio, optional CoreML/CUDA) are gated by `$([MSBuild]::IsOSPlatform(...))` conditions on the package refs. | `QuickSType.Core` + **both** platform projects + Avalonia + `Whisper.net.Runtime` + `CommunityToolkit.Mvvm`. |
| `QuickSType.Core.Tests` (`tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj`) | xunit + Shouldly tests for `Core` only. | `QuickSType.Core`. |
## Platform abstraction contract
| Port | File | Adapters |
|---|---|---|
| `IPasteService` | `src/QuickSType.Core/Paste/IPasteService.cs` (single async method `PasteAsync(text, ct)`) | `MacPasteService` (`src/QuickSType.Platform.Mac/MacPasteService.cs`) — Cmd+V via `CGEventCreateKeyboardEvent` + `CGEventPost`, falls back to `osascript` keystroke. Saves+restores clipboard around a 120 ms settle. <br> `WindowsPasteService` (`src/QuickSType.Platform.Windows/WindowsPasteService.cs`) — same envelope but `user32.SendInput` for Ctrl+V. |
| `IPermissionService` | `src/QuickSType.Core/Platform/IPermissionService.cs` (mic / input-monitoring / accessibility checks + open-settings) | `MacPermissions` (`MacPermissions.cs`) — calls `AXIsProcessTrusted` / `AXIsProcessTrustedWithOptions`; opens `x-apple.systempreferences:` URIs via `/usr/bin/open`. <br> `WindowsPermissions` (`WindowsPermissions.cs`) — all checks return true (Windows mic prompt is OS-driven); opens `ms-settings:` URIs. |
| `IAutoLaunchService` | `src/QuickSType.Core/Platform/IAutoLaunchService.cs` | `MacAutoLaunch` — writes/removes a LaunchAgent plist at `~/Library/LaunchAgents/com.saherk.quickstype.plist` and `launchctl load/unload`. <br> `WindowsAutoLaunch` — `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `QuickSType`. |
| `INotificationService` | `src/QuickSType.Core/Platform/INotificationService.cs` (`Notify`, `PlayStartAsync`, `PlayStopAsync`) | `MacNotifications` — `osascript "display notification"` + `/usr/bin/afplay` of `Tink.aiff` / `Pop.aiff`. <br> `WindowsNotifications` — PowerShell `NotifyIcon` balloon + `kernel32.Beep`. |
### Composition / DI wiring
- `OSX` branch instantiates the four `Platform.Mac.*` services (lines 76-82).
- `Windows` branch instantiates the four `Platform.Windows.*` services (lines 83-89).
- Otherwise it binds in-process **`Noop*`** stubs (`AppHost.cs:140-167`) so unit-style runs and unsupported platforms don't crash.
## Data flow / runtime pipeline
## Entry points
- **Process entry**: `src/QuickSType.UI/Program.cs:5-26`. `[STAThread] Main` calls `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)` inside a try/catch that prints to `stderr` and returns 1 on failure. `BuildAvaloniaApp` chains `AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()`.
- **Avalonia bootstrap**: `src/QuickSType.UI/App.axaml.cs:14-34`. `Initialize` loads `App.axaml`. `OnFrameworkInitializationCompleted` is the **startup sequence**:
- **Settings window**: `src/QuickSType.UI/Views/MainWindow.axaml` + `.cs`. Lazy-instantiated by `TrayService.ShowMain` (`TrayService.cs:197-216`) when the user clicks the tray; receives `AppHost` via constructor and binds `SettingsViewModel` as `DataContext`.
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
- **Source-gen JSON**: `ConfigJsonContext` in `src/QuickSType.Core/Config/JsonContext.cs:5-13` declares `[JsonSerializable(typeof(AppConfig))]` + `[JsonSerializable(typeof(LegacyPythonConfig))]` with `SnakeCaseLower` naming. Required for AOT.
- **Migration**: `MigrateFromPython` (`ConfigStore.cs:95-141`) maps the Python POC's `model` strings (`mlx-community/whisper-*` → `ggml-*`) and `hotkey` strings (`alt_r` → `VcRightAlt`, etc.). Migration is exhaustively pinned by `tests/QuickSType.Core.Tests/ConfigMigrationTests.cs` (theory tables for hotkey + model mappings, full-record migration, empty-defaults case).
- **Models**: not config — downloaded ggml `.bin` files. Catalog at `src/QuickSType.Core/Transcribe/ModelCatalog.cs` (default `ggml-large-v3-turbo-q5_0`, ~570 MB). Stored under `ModelCatalog.ModelsDirectory()` — `~/Library/Application Support/QuickSType/models` on Mac, `%LOCALAPPDATA%\QuickSType\models` on Win. `ModelDownloader.DownloadAsync` supports HTTP `Range` resume from a `*.bin.part` sidecar.
## Existing docs
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->
## Project Skills

No project skills found. Add skills to any of: `.Codex/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->



<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-Codex-profile` -- do not edit manually.
<!-- GSD:profile-end -->
