---
type: codebase-map
focus: quality
last_mapped: 2026-05-07
---

# QuickSType.NET — Testing

## Test framework

The single test project is `tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj`. From its `<PackageReference>` items:

- **xUnit 2.9.2** — `xunit` + `xunit.runner.visualstudio` 3.0.1
- **Microsoft.NET.Test.Sdk 17.12.0**
- **Shouldly 4.3.0** — *not* FluentAssertions; assertions read like `value.ShouldBe(...)`, `result.ShouldNotBeNull()`, `list.ShouldContain(...)`.
- **No mocking framework** (no Moq / NSubstitute / FakeItEasy).
- **No Microsoft.Extensions.* test packages**, no `WebApplicationFactory`, no integration-test scaffolding.

`tests/QuickSType.Core.Tests/GlobalUsings.cs` collapses the per-file imports to two `global using` statements:

```csharp
global using Xunit;
global using Shouldly;
```

The project inherits `<TargetFramework>net10.0</TargetFramework>`, `<Nullable>enable</Nullable>`, and `<LangVersion>latest</LangVersion>` from the repo-root `Directory.Build.props`. It references only `src/QuickSType.Core/QuickSType.Core.csproj` — no platform projects (`QuickSType.Platform.Mac`, `QuickSType.Platform.Windows`) and no UI project are exercised.

## Test layout

All test files live flat under `tests/QuickSType.Core.Tests/`. One test class per file, namespaced `QuickSType.Core.Tests`.

- **`tests/QuickSType.Core.Tests/WavWriterTests.cs`** — exercises `WavWriter.WritePcm16` (`src/QuickSType.Core/Audio/WavWriter.cs`). Verifies the RIFF/WAVE/fmt /data header layout, that PCM-16 samples round-trip within quantisation error, and that out-of-range floats are clamped to `±short.MaxValue`.

- **`tests/QuickSType.Core.Tests/LanguageValidationTests.cs`** — covers `Languages.Find` and the `AppConfig.WithLanguage / WithAutoLanguage / WithEnabledLanguages` pure mutators (`src/QuickSType.Core/Languages.cs`, `src/QuickSType.Core/Config/Config.cs`). Asserts case-insensitive lookup, lower-case normalisation, whitespace trimming, dedup, unknown-code filtering, default-to-English fallback, active-language preservation/shift behaviour, ordering, and `AutoLanguage` flag interactions.

- **`tests/QuickSType.Core.Tests/ConfigMigrationTests.cs`** — covers `ConfigStore.MapPythonHotkey`, `ConfigStore.MapPythonModel`, and `ConfigStore.MigrateFromPython` (`src/QuickSType.Core/Config/ConfigStore.cs`). Pins the legacy-Python-key/model translation table and the full migration shape (model + languages + active language + hotkey + auto-language + schema version 2).

- **`tests/QuickSType.Core.Tests/HotkeyParsingTests.cs`** — covers `HotkeyService.ParseKey` and `HotkeyService.Format` (`src/QuickSType.Core/Hotkey/HotkeyService.cs`). Asserts case-insensitive enum parsing, fallback-to-`VcRightAlt` on garbage, and parse/format round-trip.

- **`tests/QuickSType.Core.Tests/ConfigRoundTripTests.cs`** — exercises the `System.Text.Json` source-generated context (`src/QuickSType.Core/Config/JsonContext.cs`) by serialising a fully-populated `AppConfig` and asserting field-by-field equality on the deserialised result. Also re-checks `WithLanguage` / `WithAutoLanguage` flag behaviour.

- **`tests/QuickSType.Core.Tests/ModelCatalogTests.cs`** — covers `ModelCatalog.Default / Find / All / PathFor` (`src/QuickSType.Core/Transcribe/ModelCatalog.cs`). Pins the default model id (`ggml-large-v3-turbo-q5_0`), the catalog membership, that every URL points at huggingface.co and ends with `.bin`, and that `PathFor` resolves under a `models` directory.

## Test patterns

- **AAA** is loosely followed but compact — most tests fit in 3-6 lines so the Arrange/Act/Assert blocks aren't visually separated. Example:

  ```csharp
  // tests/QuickSType.Core.Tests/LanguageValidationTests.cs:39-45
  [Fact]
  public void With_language_normalises_case_to_lowercase()
  {
      var c = new AppConfig { ActiveLanguage = "en", Languages = new() { "en" }, AutoLanguage = false };
      var c2 = c.WithLanguage("HE");
      c2.ActiveLanguage.ShouldBe("he");
      c2.Languages.ShouldContain("he");
  }
  ```

- **Naming convention: `Method_State_ExpectedResult`** in snake_case (with underscores between words), e.g. `Common_languages_round_trip_through_find`, `Find_is_case_insensitive`, `With_language_no_ops_on_unknown_or_empty`, `Defaults_unknown_to_right_alt`, `All_urls_point_at_huggingface`. The convention is consistent across all six files.

- **`[Theory]` + `[InlineData]` is preferred over loops** for table-driven cases — `LanguageValidationTests` uses `[Theory]` for case-insensitivity probes and unknown-code rejection; `ConfigMigrationTests` uses it to pin the entire Python-to-.NET hotkey/model mapping table; `HotkeyParsingTests` does the same for the `KeyCode` enum.

- **`[Fact]` for single-shot pinning** — used for shape/contract tests like `Migrates_full_legacy_config`, `All_fields_round_trip_through_source_gen`, `Default_is_turbo_quant`.

- **Shouldly is used uniformly** — `ShouldBe`, `ShouldNotBeNull`, `ShouldContain`, `ShouldBeFalse`, `ShouldBeTrue`, `ShouldBeLessThan`, `ShouldStartWith`, `ShouldEndWith`, `ShouldBeGreaterThan`. No raw `Assert.Equal` calls.

- **No `[Trait]` categories**, no test collections, no parallelisation tweaks — vanilla xUnit defaults (per-class parallelisation).

## Mocking / fixtures

- **No mocking library is referenced.** Production services that take dependencies via constructor (`DictationEngine`, `Transcriber`, `HotkeyService`, `ConfigStore`, `PortAudioCapture`, `ModelDownloader`) are *not* unit-tested in this project — so no test doubles are needed.

- **No hand-rolled fakes** are present in `tests/`. The only "noop" implementations (`NoopPasteService`, `NoopNotifications`, `NoopPermissions`, `NoopAutoLaunch`) live in `src/QuickSType.UI/Composition/AppHost.cs:140-167` as *production* fall-throughs for unsupported OSes, not test doubles.

- **No fixtures, no `IClassFixture<T>`, no `ICollectionFixture<T>`** — every test constructs its own values inline.

- **No test data builders** — fixtures are built with C# 9+ object initializers, e.g. `new AppConfig { Languages = new() { "en" }, ActiveLanguage = "en" }` or by calling pure factories like `ConfigStore.MigrateFromPython(new LegacyPythonConfig { ... })`.

The testing strategy is **deliberately scoped to pure / static functions** — `WavWriter.WritePcm16` (static), `Languages.Find` (static), `AppConfig.With*` (record `with` mutators), `ConfigStore.Map*` and `ConfigStore.MigrateFromPython` (static), `HotkeyService.ParseKey`/`Format` (static), `ModelCatalog.*` (static). This is why the project gets away without any mocking: there is nothing to mock.

## Coverage gaps

The test suite covers **only `QuickSType.Core` pure logic**. Substantial production code has **no test file at all**:

- **Stateful Core services with side effects:**
  - `src/QuickSType.Core/DictationEngine.cs` — the orchestration class (state machine, hotkey-to-pipeline, error event surface). No tests.
  - `src/QuickSType.Core/Transcribe/Transcriber.cs` — `EnsureLoaded`, `TranscribeAsync`, `PrewarmAsync`. No tests.
  - `src/QuickSType.Core/Transcribe/ModelDownloader.cs` — HTTP download with resume, progress reporting, `.part` rename. No tests.
  - `src/QuickSType.Core/Audio/PortAudioCapture.cs` — PortAudio interop, `Start`/`Stop`/`DrainChunks`/`OnAudio`. No tests.
  - `src/QuickSType.Core/Hotkey/HotkeyService.cs` — only `ParseKey`/`Format`/`IsModifier` are reachable via tests; the actual `RunAsync`/event-loop path and `CaptureNextKeyAsync` timeout logic are uncovered.
  - `src/QuickSType.Core/Config/ConfigStore.cs` — `Load` (legacy migration on disk, corrupt-file fallback), `Save` (atomic temp+replace), and `DefaultPath` per-OS branching are uncovered. Only the static `MapPython*` and `MigrateFromPython` helpers are tested.

- **Platform projects — entire assemblies are untested:**
  - `src/QuickSType.Platform.Mac/MacPasteService.cs`
  - `src/QuickSType.Platform.Mac/MacNotifications.cs`
  - `src/QuickSType.Platform.Mac/MacPermissions.cs`
  - `src/QuickSType.Platform.Mac/MacAutoLaunch.cs`
  - `src/QuickSType.Platform.Windows/WindowsPasteService.cs`
  - `src/QuickSType.Platform.Windows/WindowsNotifications.cs`
  - `src/QuickSType.Platform.Windows/WindowsPermissions.cs`
  - `src/QuickSType.Platform.Windows/WindowsAutoLaunch.cs`

- **UI project — entire assembly is untested:**
  - `src/QuickSType.UI/Program.cs`
  - `src/QuickSType.UI/App.axaml.cs`
  - `src/QuickSType.UI/Composition/AppHost.cs` (composition root + `Noop*` fallbacks + `ConfigStoreWithExists`)
  - `src/QuickSType.UI/ViewModels/SettingsViewModel.cs`
  - `src/QuickSType.UI/Views/MainWindow.axaml.cs`
  - `src/QuickSType.UI/Tray/TrayService.cs`

- **End-to-end pipeline** — there is no integration test that wires `IAudioCapture` (fake) → `Transcriber` → `IPasteService` (fake) through `DictationEngine` to validate the state machine, the cancel-on-re-press behaviour, or the "recording too short" early-return.

In short: every file under `src/` whose name does **not** end in `.cs` for `Languages`, `Audio/WavWriter`, `Config/Config`, `Config/ConfigStore` (static helpers only), `Config/JsonContext`, `Hotkey/HotkeyService` (static helpers only), or `Transcribe/ModelCatalog` is currently uncovered by automated tests.

## CI / how to run

- **No CI is configured in the repo.** There is no `.github/workflows/` directory, no `azure-pipelines.yml`, and no `.gitlab-ci.yml`. CI status is therefore manual / local-only at the time of mapping.

- **Manual run** from the repo root:

  ```bash
  dotnet test
  ```

  This picks up `QuickSType.sln`, restores, builds Core + the test project (the test project doesn't reference the Mac/Windows platform assemblies, so it builds on any host), and runs xUnit through `xunit.runner.visualstudio` 3.0.1.

- **Targeted run**:

  ```bash
  dotnet test tests/QuickSType.Core.Tests/QuickSType.Core.Tests.csproj
  ```

- **Framework target in CI**: `net10.0` only (inherited from `Directory.Build.props`); the SDK version is pinned to `10.0.100` with `rollForward: latestFeature` in `global.json`. There is no multi-targeting, no Mono leg, and no platform matrix.

- **Publish scripts** under `build/` (`build/publish-mac.sh`, `build/publish-windows.ps1`) are app-packaging scripts, not test scripts — they do not run `dotnet test`.
