---
type: codebase-map
focus: quality
last_mapped: 2026-05-07
---

# QuickSType.NET — Conventions

## Language features in use

The codebase targets `net10.0` (see `Directory.Build.props`) with `<LangVersion>latest</LangVersion>` and the latest C# features turned on globally. A scan of representative files confirms:

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

`Directory.Build.props` (the single source of truth for cross-project compilation):

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

`global.json` pins the SDK to `10.0.100` with `rollForward: latestFeature` and `allowPrerelease: false`.

The Core library is **AOT-flagged** with `<IsAotCompatible>true</IsAotCompatible>` (`src/QuickSType.Core/QuickSType.Core.csproj:6`), which forces the Roslyn AOT analysers on for that project.

**No `.editorconfig`** is present at the repo root or under `src/` — formatting/style is implicit (matching the implicit `dotnet format` defaults: 4-space indent, Allman braces, `var` where the type is apparent).

## Naming & style

Standard .NET conventions are observed throughout, with no surprises:

- **PascalCase** for types, public members, methods, and constants.
- **camelCase** for parameters and locals (`samples`, `wavBytes`, `modelPath`, `useGpu`).
- **`_camelCase`** for private instance fields (`_log`, `_audio`, `_transcriber`, `_paste`, `_notify`, `_configStore`, `_config`, `_state`, `_cts`).
- **`I*` interfaces** — `IAudioCapture`, `IPasteService`, `INotificationService`, `IPermissionService`, `IAutoLaunchService`.
- **`s_` / `t_`** prefixes are *not* used; static fields (e.g. `InitLock`, `_initialized`) follow PascalCase or `_camelCase` mixed with `private static`.
- **`sealed` by default** — most concrete classes are `sealed` (`DictationEngine`, `HotkeyService`, `Transcriber`, `PortAudioCapture`, `ModelDownloader`, `AppHost`). `ConfigStore` is the notable non-sealed type, deliberately so that `ConfigStoreWithExists` can subclass it (`src/QuickSType.UI/Composition/AppHost.cs:132`).

Representative snippets:

```csharp
// src/QuickSType.Core/DictationEngine.cs:21-29
private readonly ILogger _log;
private readonly IAudioCapture _audio;
private readonly Transcriber _transcriber;
private readonly IPasteService _paste;
private readonly INotificationService _notify;
private readonly ConfigStore _configStore;
private AppConfig _config;
private DictationState _state = DictationState.Idle;
private CancellationTokenSource? _cts;
```

```csharp
// src/QuickSType.Core/Languages.cs:27-31
public static LanguageInfo? Find(string code) =>
    Common.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

public static string DisplayFor(string code) =>
    Find(code) is { } info ? info.DisplayName : code.ToUpperInvariant();
```

```csharp
// src/QuickSType.Core/Hotkey/HotkeyService.cs:86-93
private static bool IsModifier(KeyCode k) => k switch
{
    KeyCode.VcLeftShift or KeyCode.VcRightShift
        or KeyCode.VcLeftControl or KeyCode.VcRightControl
        or KeyCode.VcLeftAlt or KeyCode.VcRightAlt
        or KeyCode.VcLeftMeta or KeyCode.VcRightMeta => true,
    _ => false,
};
```

`String.Equals` is consistently called with `StringComparison.OrdinalIgnoreCase` for code/key matching (`Languages.cs:28`, `ModelCatalog.cs:26`, `PortAudioCapture.cs:69`). Lowercase normalisation goes through `.Trim().ToLowerInvariant()` (`Config.cs:39`, `Config.cs:66`).

## Error handling pattern

The dominant pattern is **catch-log-recover-and-surface-via-event**, not exception bubbling and not `Result<T>` types. Layers behave as follows:

- **Long-lived services** (`DictationEngine`, `HotkeyService`) wrap user-callable entry points in `try / catch (Exception ex)`, log via `ILogger.LogError`, then either swallow (for handler invocations) or re-emit through a `public event Action<Exception>? Errored`.

  ```csharp
  // src/QuickSType.Core/DictationEngine.cs:73-83
  try
  {
      _audio.Start();
      SetState(DictationState.Recording);
      _ = _notify.PlayStartAsync();
  }
  catch (Exception ex)
  {
      _log.LogError(ex, "Failed to start recording");
      Errored?.Invoke(ex);
  }
  ```

- **Pipeline tasks** use a top-level `try / catch / finally` to guarantee state reset:

  ```csharp
  // src/QuickSType.Core/DictationEngine.cs:103-150
  catch (Exception ex)
  {
      _log.LogError(ex, "Dictation pipeline failed");
      Errored?.Invoke(ex);
  }
  finally
  {
      SetState(DictationState.Idle);
  }
  ```

- **Event-handler invocations are wrapped defensively** so a misbehaving subscriber cannot crash the engine: `try { StateChanged?.Invoke(s); } catch (Exception ex) { _log.LogError(ex, "StateChanged handler threw"); }` at `src/QuickSType.Core/DictationEngine.cs:157-158`, mirrored in `HotkeyService.cs:71-72,81-82`.

- **`Dispose` swallows** to avoid throwing in finalisers/cleanup: `catch { /* swallow on dispose */ }` (`HotkeyService.cs:111`, `PortAudioCapture.cs:166`, `AppHost.cs:121`).

- **Invariant-violation throws** use stock BCL exception types — no custom exception hierarchy exists. Examples: `throw new InvalidOperationException("Model not loaded. Call EnsureLoaded() first.")` at `src/QuickSType.Core/Transcribe/Transcriber.cs:43`, and `Errored?.Invoke(new FileNotFoundException("Whisper model not installed.", modelPath))` at `src/QuickSType.Core/DictationEngine.cs:118`.

- **Recoverable load failures** log a warning and fall back: `ConfigStore.Load()` at `src/QuickSType.Core/Config/ConfigStore.cs:48-58` catches a corrupt config and returns a fresh `AppConfig`.

There is **no** `Result<T>` / `Either` / `OneOf` style in use, and no try-pattern (`bool TryX(out _)`) outside of BCL `Enum.TryParse` (`HotkeyService.cs:97`).

## Logging

`Microsoft.Extensions.Logging` is the only abstraction in use (`Microsoft.Extensions.Logging.Abstractions` v10.0.0 referenced at `src/QuickSType.Core/QuickSType.Core.csproj:11`).

- Every service that takes a logger accepts an **optional** `ILogger<T>?` and falls back to `NullLogger.Instance`, so Core types can be unit-tested or instantiated without a logger factory:

  ```csharp
  // src/QuickSType.Core/DictationEngine.cs:53
  _log = (ILogger?)log ?? NullLogger.Instance;
  ```
  Same pattern in `HotkeyService.cs:24`, `Transcriber.cs:18`, `ConfigStore.cs:18`, `PortAudioCapture.cs:27`, `ModelDownloader.cs:17`.

- The composition root (`AppHost.Create`) builds a single `ILoggerFactory` with `AddConsole` at `LogLevel.Information` (`src/QuickSType.UI/Composition/AppHost.cs:58-62`) and hands `CreateLogger<T>()` to each service.

- **`Console.WriteLine` is reserved for fatal-path output only** — used once in `Program.Main` for an unhandled top-level exception (`src/QuickSType.UI/Program.cs:16`).

- Log calls use **structured-message templates with named placeholders**, never string interpolation: `_log.LogInformation("Loaded Whisper model from {Path}", modelPath)`, `_log.LogDebug("Press ignored; state is {State}", _state)`, `_log.LogWarning(ex, "Error stopping PortAudio stream")`. This keeps the call sites AOT/source-generator friendly.

## Async / cancellation

- All I/O-bound work is `async Task` / `Task<T>`. Examples: `Transcriber.TranscribeAsync(ReadOnlyMemory<float> samples, int sampleRate, AppConfig config, CancellationToken cancellationToken = default)` (`src/QuickSType.Core/Transcribe/Transcriber.cs:36-41`), `ModelDownloader.DownloadAsync(ModelInfo model, IProgress<Progress>? progress = null, CancellationToken cancellationToken = default)` (`src/QuickSType.Core/Transcribe/ModelDownloader.cs:25-29`), `IPasteService.PasteAsync(string text, CancellationToken cancellationToken = default)` (`src/QuickSType.Core/Paste/IPasteService.cs:5`).

- **`CancellationToken` propagation is consistent** down through the stack — `DictationEngine` owns a `CancellationTokenSource _cts` (`DictationEngine.cs:29`), cancels-and-replaces on each release (`DictationEngine.cs:98-100`), and forwards the token into `Transcriber.TranscribeAsync` and `IPasteService.PasteAsync`. `ModelDownloader` cooperatively checks `cancellationToken.ThrowIfCancellationRequested()` inside its read loop (`ModelDownloader.cs:73`).

- **Fire-and-forget is explicit**: discarded with `_ =` rather than ignored, e.g. `_ = _notify.PlayStartAsync();` (`DictationEngine.cs:77`), `_ = ProcessAsync(samples, _cts.Token);` (`DictationEngine.cs:100`), `_ = Hotkey.RunAsync();` (`AppHost.cs:111`).

- **No `ConfigureAwait(false)`** — the project relies on the .NET 8+ default `ConfigureAwait` semantics for library code; this is acceptable here because the desktop app uses Avalonia's UI dispatcher and there is no synchronisation-context mismatch hot-path. *Worth noting* if the library is ever consumed in an ASP.NET Core context.

- **No `async void`** outside of (implicit) UI event handlers in the Avalonia layer.

Representative signature:

```csharp
// src/QuickSType.Core/Transcribe/Transcriber.cs:36-41
public async Task<string> TranscribeAsync(
    ReadOnlyMemory<float> samples,
    int sampleRate,
    AppConfig config,
    CancellationToken cancellationToken = default)
```

## DI / composition

There is **no `Microsoft.Extensions.DependencyInjection` container**. Composition is **manual / poor-man's-DI** assembled in a single static factory, `AppHost.Create()` (`src/QuickSType.UI/Composition/AppHost.cs:56-105`):

1. Build one `ILoggerFactory`.
2. Construct the `ConfigStoreWithExists` and load `AppConfig`.
3. Construct concrete `PortAudioCapture`, `Transcriber`, `ModelDownloader`.
4. Branch on `RuntimeInformation.IsOSPlatform(OSPlatform.OSX | Windows | else)` to pick `MacPasteService / WindowsPasteService / NoopPasteService` (and similarly for `INotificationService`, `IPermissionService`, `IAutoLaunchService`).
5. Wire `HotkeyService.Pressed/Released` events to `DictationEngine.OnHotkeyPressed/Released`.
6. Return a single `AppHost` aggregate that owns and exposes the graph.

The pattern keeps Core dependency-free apart from `Microsoft.Extensions.Logging.Abstractions`, which is critical for the AOT/`<IsAotCompatible>true</IsAotCompatible>` setting on the Core project.

Linux / unsupported-OS fall-through uses the inline `Noop*` services defined at the bottom of `AppHost.cs:140-167`, so the engine can run on Linux without crashing on missing platform integrations (paste/notify silently no-op there).

## JSON / serialization

- **`System.Text.Json`** with the **source-generator** is the only JSON path. The single context lives at `src/QuickSType.Core/Config/JsonContext.cs`:

  ```csharp
  [JsonSourceGenerationOptions(
      WriteIndented = true,
      PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
      DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
  [JsonSerializable(typeof(AppConfig))]
  [JsonSerializable(typeof(LegacyPythonConfig))]
  public partial class ConfigJsonContext : JsonSerializerContext { }
  ```

- Snake-case property names are produced by the generator; per-property `[JsonPropertyName]` attributes are also set on every field on `AppConfig` (e.g. `[JsonPropertyName("active_language")]` at `src/QuickSType.Core/Config/Config.cs:13`) — this is belt-and-braces but useful as documentation.

- `ConfigStore.Load` / `Save` go through `ConfigJsonContext.Default.AppConfig` (and `.LegacyPythonConfig` for the migration path) — never `JsonSerializer.Serialize<T>(...)` with a runtime-reflection overload. This is what keeps the Core library AOT-compatible (`<IsAotCompatible>true</IsAotCompatible>` in `QuickSType.Core.csproj`).

- **Atomic save**: `ConfigStore.Save` writes `<path>.tmp` first, then `File.Replace`/`File.Move` — see `src/QuickSType.Core/Config/ConfigStore.cs:88-92`. This avoids leaving a half-written config on crash.

- **AOT considerations**: no reflection-based serialization, no expression trees in `JsonSerializer` calls, no `JsonElement` parsing. Adding a new persisted type means adding a `[JsonSerializable(typeof(T))]` line to `ConfigJsonContext` and routing through `ConfigJsonContext.Default.T` — the generator handles trim/AOT safety. New polymorphic shapes would need explicit `[JsonDerivedType]` attributes.
