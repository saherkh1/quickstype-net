# Phase 8: Per-Language Model Selection — Research

**Researched:** 2026-05-24
**Domain:** AppConfig schema extension, ModelCatalog, Transcriber swap-on-demand, DictationEngine state, Avalonia Settings UI
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Per-language model picker lives as a gear/cog button on each enabled language row in the Languages tab. Clicking the gear opens a modal/flyout (implementation choice left to Claude's discretion based on Avalonia patterns already in the codebase).
- **D-02:** Language rows do NOT show the currently-assigned model name inline — only the gear icon is shown. Model detail is shown inside the flyout only.
- **D-03:** The existing global model picker (Models tab) stays clean — lists only standard global models. Fine-tuned language-specific models appear only in the per-language flyout, never in the global list.
- **D-04:** The per-language flyout includes a download button + progress bar inline (reusing existing `ModelDownloader` + progress pattern). No separate navigation required.
- **D-05:** Hebrew fine-tuned model added as a named entry in `ModelCatalog.All` (ID: `ggml-medium-he`, URL: `https://huggingface.co/imvladikon/whisper-medium-he/resolve/main/ggml-hebrew.bin`). Tagged to the `"he"` language code so it only appears in the Hebrew flyout.
- **D-06:** Arabic has no curated ggml fine-tune available. Arabic entry in the per-language flyout shows only the global models. No placeholder or broken entry — just "Use global model" as the only option.
- **D-07:** `ModelCatalog` gains a `LanguageModels` dictionary: `Dictionary<string, IReadOnlyList<ModelInfo>>` mapping language codes to their curated fine-tuned entries. Global models are NOT in this map.
- **D-08:** `AppConfig` gains a `language_models` field: `Dictionary<string, string>` (snake_case JSON key), serialized via `ConfigJsonContext` source-gen. Default is an empty dictionary. SchemaVersion bumps from `3` → `4`. Migration: v3 configs load cleanly — empty `language_models` means "use global for all languages."
- **D-09:** Model loading is **lazy**: DictationEngine resolves the active language's assigned model just before `EnsureLoaded()`. If it differs from the currently loaded model, it reloads at that point. No background pre-warm.
- **D-10:** While the model is loading at dictation start, the HUD shows a "Loading model…" state. Recording begins only after `EnsureLoaded()` completes.
- **D-11:** If the user releases the hotkey while the model is still loading, the load is cancelled (via `CancellationToken`), the engine returns to `Idle`, and nothing is transcribed.
- **D-12:** When `AutoLanguage=true`, the per-language model map does NOT apply. DictationEngine uses the global model only.

### Claude's Discretion

- Whether to implement the flyout as an Avalonia `Popup` (anchored to gear button) or as a lightweight `Dialog` window.
- Thread management for wrapping `Transcriber.EnsureLoaded()` (sync, locked) in `Task.Run` for the lazy-load path.
- Whether to add a new `DictationEngine` state variant for "loading model" or reuse an existing transitional state.
- Exact `ConfigStore.MigrateV3ToV4` logic (straightforward: inject empty `language_models: {}` and bump version).

### Deferred Ideas (OUT OF SCOPE)

- Dynamic HF model browser for per-language community model discovery.
- Arabic fine-tuned ggml model (none available yet).
- Auto-language → detected-language model-swap during `AutoLanguage=true` sessions.
- More languages beyond Arabic/Hebrew in the per-language fine-tune catalog.
</user_constraints>

---

## Summary

Phase 8 extends three existing subsystems with minimal new surface area: (1) the `AppConfig` record gains a `language_models: Dictionary<string, string>` field and a schema version bump from 3→4; (2) `ModelCatalog` gains a `LanguageModels` static dictionary and the Hebrew fine-tuned entry; (3) `DictationEngine.ProcessAsync` / `OnHotkeyPressed` gains pre-transcription model resolution with a new `LoadingModel` state visible in the HUD; (4) the Languages tab in `MainWindow.axaml` gains a gear button per row that opens a per-language model flyout.

The Hebrew model URL `https://huggingface.co/imvladikon/whisper-medium-he/resolve/main/ggml-hebrew.bin` is confirmed reachable and returns HTTP 302 to an S3 presigned URL (standard HF LFS pattern). The file size is confirmed at `1,533,763,059 bytes` (identical to `ggml-medium`, as expected for a medium-size fine-tune). The SHA-256 cannot be obtained without downloading the full file; the plan must include a step to compute and pin it before shipping.

The biggest implementation risk is the Avalonia `Popup`-vs-`Dialog` decision combined with compiled bindings — `LanguageRow` currently has no command property, and adding a gear button with a flyout requires extending the VM before extending the XAML.

**Primary recommendation:** Add `LoadingModel` as a new `DictationState` enum value. Use an Avalonia `Window`-style modal dialog (matching the `BlockerDialogWindow` pattern already in the codebase) rather than a `Popup`, because `Popup` in Avalonia 11.2 with compiled bindings is more complex and has focus/dismiss edge cases on macOS.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `language_models` config field + migration | Core (`Config/Config.cs`, `ConfigStore.cs`, `JsonContext.cs`) | — | Config is owned entirely by Core |
| Hebrew model catalog entry + `LanguageModels` map | Core (`Transcribe/ModelCatalog.cs`) | — | Catalog is a pure Core static type |
| manifest.json entry for `ggml-medium-he` + SHA256 pin | Build artifact (`models/manifest.json`) | Core (ModelManifestLoader reads it) | Manifest is a build-time artifact consumed by Core |
| Model resolution per active language | Core (`DictationEngine.ProcessAsync`) | — | All dictation pipeline logic lives in Core |
| `LoadingModel` HUD state | Core (`DictationState` enum) + UI (`HudViewModel`, `HudWindow`) | — | Enum defined in Core; UI renders it |
| `Transcriber.EnsureLoaded` async wrapper | Core (`DictationEngine`) | — | Must run off UI thread via `Task.Run` |
| Per-language flyout UI + `LanguageRow` gear command | UI (`SettingsViewModel`, `MainWindow.axaml`) | — | Pure UI concern; Core provides catalog data |
| Model download in flyout | UI (reuse existing `ModelDownloader` + progress pattern) | Core (`ModelDownloader.DownloadAsync`) | Download is a Core service; UI wires progress |

---

## Standard Stack

No new NuGet packages required. Phase 8 is entirely within the existing stack.

### Core (all existing, no changes to package refs)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Text.Json` source-gen | BCL .NET 10 | AOT-safe JSON for `Dictionary<string, string>` | Already used via `ConfigJsonContext`; `Dictionary<string, string>` is natively supported by STJ source-gen when the parent record is registered |
| `CommunityToolkit.Mvvm` | 8.4.2 | `[ObservableProperty]` + `[RelayCommand]` on `LanguageRow` and new flyout VM | Pattern already used by `SettingsViewModel` and `ModelRowViewModel` |
| `Whisper.net` | 1.9.0 | Model loading in `Transcriber.EnsureLoaded` | Unchanged; lazy swap already works via `_loadedModelPath` comparison |

### Package Legitimacy Audit

> No new packages are introduced in Phase 8. All code uses existing dependencies.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| (none new) | — | — | — | — | — | — |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

---

## Architecture Patterns

### System Architecture Diagram

```
Hotkey Press
     │
     ▼
DictationEngine.OnHotkeyPressed()
     │
     ├─ AutoLanguage=true? ──► use global config.Model (unchanged path)
     │
     └─ AutoLanguage=false
          │
          ▼
     ResolveModelId(config)
     [config.LanguageModels.TryGetValue(config.ActiveLanguage) ?? config.Model]
          │
          ▼
     resolved model ID same as _transcriber.LoadedModelPath?
          │
      YES │                      NO
          │                       │
          ▼                       ▼
     SetState(Recording)    SetState(LoadingModel) ──► HUD shows "Loading model…"
     [existing path]              │
                                  ▼
                           await Task.Run(() =>
                             _transcriber.EnsureLoaded(modelPath, useGpu), ct)
                                  │
                           hotkey released during load?
                                  │
                             YES  │  NO
                                  │   │
                                  ▼   ▼
                             Idle    SetState(Recording / Streaming)
                                     [continue normal pipeline]
```

### Recommended Project Structure

No new folders required. Changes are surgically within existing files:

```
src/QuickSType.Core/
├── Config/
│   ├── Config.cs            # + LanguageModels field + WithLanguageModel()
│   ├── ConfigStore.cs       # + MigrateV3ToV4()
│   └── JsonContext.cs       # + [JsonSerializable(typeof(Dictionary<string, string>))]
├── Transcribe/
│   └── ModelCatalog.cs      # + LanguageModels dict + ggml-medium-he entry
├── DictationEngine.cs       # + ResolveModelId() + LoadingModel state handling
└── (DictationState enum, same file)

src/QuickSType.UI/
├── ViewModels/
│   ├── SettingsViewModel.cs # + LanguageRow.OpenModelPickerCommand + LanguageModelFlyoutViewModel
│   └── LanguageModelFlyoutViewModel.cs  # NEW (small VM for flyout)
└── Views/
    ├── MainWindow.axaml     # + gear button per language row
    └── LanguageModelFlyoutWindow.axaml  # NEW (modal dialog for model picker)

models/manifest.json         # + ggml-medium-he entry (SHA256 + pinned URL)
```

### Pattern 1: AppConfig Field Addition (established pattern)

**What:** Add `language_models` as a property with `[JsonPropertyName]` and a `with`-expression mutation helper.
**When to use:** Whenever a new persisted setting is added to `AppConfig`.

```csharp
// Source: src/QuickSType.Core/Config/Config.cs (established pattern — mirror PreferredModel)
[JsonPropertyName("language_models")]
public Dictionary<string, string> LanguageModels { get; init; } = new();

/// <summary>
/// Returns a copy of this config with the specified language mapped to <paramref name="modelId"/>.
/// Pass null modelId to remove the language-specific mapping (fall back to global model).
/// </summary>
public AppConfig WithLanguageModel(string langCode, string? modelId)
{
    var next = new Dictionary<string, string>(LanguageModels, StringComparer.OrdinalIgnoreCase);
    if (modelId is null)
        next.Remove(langCode);
    else
        next[langCode] = modelId;
    return this with { LanguageModels = next };
}
```

### Pattern 2: MigrateV3ToV4 (established migration pattern)

**What:** Inject empty `LanguageModels` and bump `SchemaVersion` to 4.
**When to use:** Match exact shape of `MigrateV2ToV3`.

```csharp
// Source: src/QuickSType.Core/Config/ConfigStore.cs (mirror MigrateV2ToV3)
public static AppConfig MigrateV3ToV4(AppConfig v3)
{
    return v3 with
    {
        LanguageModels = new Dictionary<string, string>(),
        SchemaVersion = 4,
    };
}
```

The `Load()` method's migration check must be extended:

```csharp
// In ConfigStore.Load():
if (cfg.SchemaVersion < 3)
{
    _log.LogInformation("Migrating config from v{OldVersion} to v3", cfg.SchemaVersion);
    cfg = MigrateV2ToV3(cfg);
    // fall through to v4 check
}
if (cfg.SchemaVersion < 4)
{
    _log.LogInformation("Migrating config from v3 to v4");
    cfg = MigrateV3ToV4(cfg);
    Save(cfg);
}
```

### Pattern 3: ModelCatalog Extension

**What:** Add `LanguageModels` static dictionary and Hebrew model entry. The Hebrew entry also goes into `ModelCatalog.All` so it is discoverable by `ModelCatalog.Find()` and `ModelCatalog.PathFor()` — the same infrastructure `DictationEngine.ProcessAsync` uses.

```csharp
// Source: src/QuickSType.Core/Transcribe/ModelCatalog.cs (NEW)
private const string HfHe = "https://huggingface.co/imvladikon/whisper-medium-he/resolve/main";

// Added to ModelCatalog.All:
new("ggml-medium-he", "Hebrew Medium (1.5 GB) — fine-tuned",
    $"{HfHe}/ggml-hebrew.bin",
    1_533_763_059,
    "Community fine-tune for Hebrew. Significantly better accuracy than the global model for Hebrew speech."),

// New property:
public static readonly Dictionary<string, IReadOnlyList<ModelInfo>> LanguageModels =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["he"] = new ModelInfo[] { Find("ggml-medium-he")! },
        // "ar" intentionally absent — Arabic uses global only (D-06)
    };
```

**Important:** `ModelCatalog.Find()` iterates `All`. Since the Hebrew model is appended to `All`, `Find("ggml-medium-he")` will work. `ModelCatalog.PathFor("ggml-medium-he")` returns `<models-dir>/ggml-medium-he.bin`. `ModelCatalog.IsInstalled("ggml-medium-he")` works with no changes.

### Pattern 4: DictationEngine Model Resolution

**What:** Extract model resolution from `ProcessAsync` into a helper, then call it from both `ProcessAsync` (commit-on-pause path) and `OnHotkeyPressed` (streaming path). The lazy-load with `LoadingModel` state must wrap the synchronous `EnsureLoaded` in `Task.Run`.

```csharp
// Source: src/QuickSType.Core/DictationEngine.cs (new helper)
internal string ResolveModelId(AppConfig cfg)
{
    if (!cfg.AutoLanguage
        && cfg.LanguageModels.TryGetValue(cfg.ActiveLanguage, out var langModel)
        && !string.IsNullOrEmpty(langModel))
    {
        return langModel;
    }
    return cfg.Model;
}
```

In `ProcessAsync`, replace the current hard-coded `_config.Model` reference:

```csharp
// OLD (line 312 of DictationEngine.cs):
var modelPath = ModelCatalog.PathFor(_config.Model);

// NEW:
var modelId = ResolveModelId(_config);
var modelPath = ModelCatalog.PathFor(modelId);
```

For the `LoadingModel` state (D-10), the lazy-load path requires:

```csharp
// In ProcessAsync, after resolving modelPath:
if (_transcriber.LoadedModelPath != modelPath)
{
    SetState(DictationState.LoadingModel);
    await Task.Run(() => _transcriber.EnsureLoaded(modelPath, useGpu), ct);
    // D-11: if hotkey released during load, ct is cancelled → OperationCanceledException → finally → Idle
}
```

### Pattern 5: JsonContext Registration for Dictionary<string, string>

**What:** The STJ source-gen for `AppConfig` will handle `Dictionary<string, string>` as a property automatically IF the type is registered. Because `Dictionary<string, ModelManifestEntry>` already works on `ModelManifest` without an explicit `[JsonSerializable(typeof(Dictionary<string, ModelManifestEntry>))]` attribute (confirmed: no such attribute exists in `JsonContext.cs`), `Dictionary<string, string>` on `AppConfig` will likewise be inferred automatically.

**Verification:** `ModelManifest.Entries` is `Dictionary<string, ModelManifestEntry>` and `ModelManifest` is registered via `[JsonSerializable(typeof(ModelManifest))]`. This compiles and round-trips correctly today without any additional attribute. The same pattern applies for `AppConfig.LanguageModels`.

However, to be explicit and defensive (AOT is unforgiving), add the explicit registration:

```csharp
// Source: src/QuickSType.Core/Config/JsonContext.cs (DEFENSIVE addition)
[JsonSerializable(typeof(Dictionary<string, string>))]
```

This is belt-and-braces but costs nothing and removes any ambiguity in AOT trim analysis.

### Pattern 6: Settings Flyout — Avalonia Dialog Window

**What:** The flyout for the per-language model picker. Given the codebase uses `BlockerDialogWindow` (a plain `Window` shown modally) as the only dialog pattern, and given that Avalonia `Popup` with compiled bindings requires careful `DataType` threading, the recommendation is to use a `Window`-style modal.

```csharp
// Pattern: LanguageModelFlyoutWindow shown from SettingsViewModel command
// Called from SettingsViewModel (on UI thread, via RelayCommand):
var flyout = new LanguageModelFlyoutWindow(langCode, _host);
await flyout.ShowDialog(mainWindow);   // ShowDialog blocks until closed
```

`ShowDialog` on Avalonia returns a `Task` that completes when the window closes, and it modal-blocks the parent — identical to what `BlockerDialogWindow` does. The `MainWindow` reference is available via `Avalonia.Application.Current?.ApplicationLifetime` cast to `IClassicDesktopStyleApplicationLifetime`.

### Pattern 7: LanguageRow Extension

**What:** `LanguageRow` needs a `[RelayCommand]` to open the flyout. Since it is a `sealed partial class ObservableObject`, adding a `[RelayCommand]` is straightforward:

```csharp
public sealed partial class LanguageRow : ObservableObject
{
    // Existing...
    
    // NEW: injected reference so the command can open the flyout
    private readonly Func<Task>? _openModelPicker;
    
    public LanguageRow(string code, string displayName, string nativeName, bool isEnabled,
        Func<Task>? openModelPicker = null)
    {
        // ...existing init...
        _openModelPicker = openModelPicker;
    }
    
    [RelayCommand]
    private async Task OpenModelPicker()
    {
        if (_openModelPicker is not null)
            await _openModelPicker();
    }
}
```

The `Func<Task>` is passed by `SettingsViewModel` when it constructs each `LanguageRow`, giving access to `_host` and `Window` reference.

### Anti-Patterns to Avoid

- **EnsureLoaded on the UI thread:** `Transcriber.EnsureLoaded` is synchronous and acquires `_lock`. Calling it directly from `OnHotkeyPressed` (which runs on the SharpHook thread-pool) is acceptable since that thread is not the UI thread. But if model resolution is ever triggered from a `RelayCommand` (UI), it MUST be wrapped in `Task.Run`.
- **Blocking `ProcessAsync` on old model:** If a language-specific model is large and the user switches language frequently, EnsureLoaded will always reload. The existing `_loadedModelPath == modelPath` check prevents unnecessary reloads — do not bypass it.
- **Adding Hebrew model to global `AvailableModels`:** D-03 explicitly prohibits this. The `AvailableModels` collection in `SettingsViewModel` is built from `ModelCatalog.All` — this means the Hebrew entry will appear there unless it is excluded. **Fix:** Filter `ModelCatalog.All` to exclude language-specific models when building `AvailableModels`. Add a property or tag on `ModelInfo` to distinguish language-specific from global models. Recommended: add `string? LanguageCode` to `ModelInfo` (nullable — null means global).
- **Forgetting manifest.json entry:** `ModelDownloader.DownloadAsync` calls `manifest.GetSha256(model.Id)` and throws `ArgumentException` if the model ID is absent. `ggml-medium-he` MUST have an entry in `models/manifest.json` with a verified SHA-256 before the download path works.
- **LanguageModels default as null:** Deserializing a v3 JSON (no `language_models` key) into the new `AppConfig` must not produce `null` for `LanguageModels`. Ensure the property uses `= new()` as the default initializer — STJ source-gen will leave it as the default when the key is absent from JSON.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Model download with SHA-256 verify + resume | Custom HTTP downloader | `ModelDownloader.DownloadAsync(ModelInfo, IProgress<Progress>?, CancellationToken)` | Already handles HF LFS redirect, range resume, SHA-256 verify via `manifest.json` |
| JSON serialization of `Dictionary<string, string>` | Custom converter | STJ source-gen on `ConfigJsonContext` | `ModelManifest.Entries` already uses `Dictionary<string, ModelManifestEntry>` via the same mechanism |
| Model path resolution | Custom path builder | `ModelCatalog.PathFor(modelId)` | Handles platform-specific paths and `.bin` extension |
| Download progress tracking | Custom progress | `IProgress<ModelDownloader.Progress>` with `Percent`, `DownloadedBytes`, `BytesPerSecond` | Existing shape reused in `SettingsViewModel.DownloadSelectedModelCommand` |
| Model availability check | File.Exists inline | `ModelCatalog.IsInstalled(modelId)` | Encapsulates path logic |

**Key insight:** The download + SHA-256 + resume infrastructure is already fully built and battle-tested. Phase 8 only needs to supply a new `ModelInfo` entry and a manifest.json line. The download UX in the flyout is a direct copy of the `DownloadSelectedModelCommand` pattern from `SettingsViewModel`.

---

## Common Pitfalls

### Pitfall 1: Hebrew Model Missing from manifest.json
**What goes wrong:** `ModelDownloader.DownloadAsync("ggml-medium-he", ...)` calls `manifest.GetSha256("ggml-medium-he")` which throws `ArgumentException: Model id 'ggml-medium-he' not present in manifest`. The download button shows an unhandled exception.
**Why it happens:** `models/manifest.json` lists only the standard global models. The Hebrew entry is a new addition that must be computed and pinned.
**How to avoid:** Plan 08-02 must include a concrete step to download the file, compute SHA-256 (`shasum -a 256` on macOS), and add the entry. The test `Every_catalog_model_has_a_manifest_entry` in `ModelManifestTests.cs` will catch this at test time.
**Warning signs:** `ArgumentException` in `ModelDownloader` log at download time.

### Pitfall 2: Hebrew Model Appearing in Global Model Picker (D-03 violation)
**What goes wrong:** After adding `ggml-medium-he` to `ModelCatalog.All`, `SettingsViewModel`'s constructor builds `AvailableModels` from `ModelCatalog.All.Select(...)` — the Hebrew model appears in the global Models tab dropdown.
**Why it happens:** `ModelCatalog.All` is used for both global and language-specific entries.
**How to avoid:** Add a `string? LanguageCode` property to `ModelInfo`. Filter `AvailableModels` to `ModelCatalog.All.Where(m => m.LanguageCode is null)`. The per-language flyout uses `ModelCatalog.LanguageModels[langCode]` instead.
**Warning signs:** Visual regression in the Models tab — "Hebrew Medium" appears in the dropdown.

### Pitfall 3: Dictionary<string, string> Deserializing as null on v3 Config Load
**What goes wrong:** A user with a v3 config file loads the app. The `language_models` key is absent from JSON. STJ source-gen sets the property to... depends on implementation. If no initializer is set, it may deserialize as `null`, causing `NullReferenceException` on `LanguageModels.TryGetValue(...)`.
**Why it happens:** STJ source-gen for records handles missing properties differently depending on whether the property has a default value.
**How to avoid:** Declare `public Dictionary<string, string> LanguageModels { get; init; } = new();`. The `= new()` initializer is the default value; STJ will use it when the key is absent. Also add a round-trip test that deserializes JSON without the `language_models` key and asserts `LanguageModels.Count == 0` (not null).
**Warning signs:** `NullReferenceException` on first hotkey press after migrating from v3.

### Pitfall 4: EnsureLoaded Blocking Hotkey-Released Cancellation
**What goes wrong:** User presses hotkey → `LoadingModel` state → `EnsureLoaded` begins (model is 1.5 GB, takes several seconds to mmap) → user releases hotkey → `_cts.Cancel()` → cancellation token is checked... but `EnsureLoaded` does not accept a `CancellationToken`.
**Why it happens:** `Transcriber.EnsureLoaded(string modelPath, bool useGpu)` is synchronous with no cancellation. `Task.Run(() => EnsureLoaded(...), ct)` will schedule the work but `ct` only cancels **scheduling**, not the running synchronous operation.
**How to avoid:** The correct approach is `await Task.Run(() => _transcriber.EnsureLoaded(modelPath, useGpu)).WaitAsync(ct)` — `WaitAsync(ct)` cancels the awaiter when the token fires, unblocking the caller. The `Task.Run` work continues in the background (mmap is idempotent), but `ProcessAsync` / `OnHotkeyPressed` returns early via `OperationCanceledException`. The model ends up loaded for the next dictation at no extra cost.
**Warning signs:** Holding hotkey, releasing immediately, then noticing the app does not return to `Idle` until EnsureLoaded finishes.

### Pitfall 5: LanguageModels Dictionary with Case-Sensitive Keys
**What goes wrong:** Config JSON written with `"en"` as the language key, but `_config.ActiveLanguage` is somehow `"EN"` (unlikely but possible if serialization ordering changes). `TryGetValue("EN")` on a case-sensitive dictionary misses the entry.
**Why it happens:** `Dictionary<string, string>` defaults to `StringComparer.Ordinal` (case-sensitive). Language codes are always lowercase in the codebase (`WithLanguage` normalizes them), but defensive coding matters.
**How to avoid:** When constructing or cloning the `LanguageModels` dictionary in `WithLanguageModel`, use `StringComparer.OrdinalIgnoreCase`. When STJ deserializes the dictionary from JSON, it uses the default comparer — reconstruct with case-insensitive comparer in `MigrateV3ToV4` and in the property initializer via a custom getter (or accept that all writes are lowercase and document the invariant).
**Warning signs:** Language-specific model assignment silently ignored.

### Pitfall 6: manifest.json ModelManifestTests Regression
**What goes wrong:** After adding `ggml-medium-he` to `ModelCatalog.All`, the existing test `Every_catalog_model_has_a_manifest_entry` fails because `models/manifest.json` does not yet have the entry.
**Why it happens:** Test verifies all catalog models have manifest entries.
**How to avoid:** Plan 08-02 (manifest.json update) MUST precede or be co-delivered with Plan 08-01 (ModelCatalog entry). Since both are in the same phase, the plan execution order must place 08-02's manifest.json change before or alongside the catalog addition.

---

## Code Examples

### Resolved Model ID Helper

```csharp
// Source: DictationEngine.cs — internal helper for D-09 / D-12
internal string ResolveModelId(AppConfig cfg)
{
    // D-12: AutoLanguage bypasses per-language map entirely
    if (cfg.AutoLanguage) return cfg.Model;

    if (cfg.LanguageModels.TryGetValue(cfg.ActiveLanguage, out var langModel)
        && !string.IsNullOrEmpty(langModel)
        && ModelCatalog.Find(langModel) is not null)
    {
        return langModel;
    }
    return cfg.Model;  // fallback to global
}
```

### ProcessAsync Integration Point

```csharp
// Source: DictationEngine.ProcessAsync (modified lines 312-322)
// BEFORE (current):
var modelPath = ModelCatalog.PathFor(_config.Model);

// AFTER (Phase 8):
var modelId = ResolveModelId(_config);
var modelPath = ModelCatalog.PathFor(modelId);
if (!File.Exists(modelPath))
{
    // ... same error handling as before but with resolved modelId
}

var useGpu = _config.TranscriptionBackend != "cpu";
if (_transcriber.LoadedModelPath != modelPath)
{
    SetState(DictationState.LoadingModel);
    await Task.Run(() => _transcriber.EnsureLoaded(modelPath, useGpu)).WaitAsync(ct);
}
// Model now loaded; continue to TranscribeAsync
```

### HudViewModel LoadingModel State

```csharp
// Source: HudViewModel.OnStateChanged (extended for LoadingModel)
public void OnStateChanged(DictationState state)
{
    IsActive = state is DictationState.Recording or DictationState.Streaming or DictationState.LoadingModel;
    IsRecording = state == DictationState.Recording;
    IsStreaming = state == DictationState.Streaming;
    IsLoadingModel = state == DictationState.LoadingModel;   // NEW property

    if (IsActive && !_elapsedTimer.IsEnabled)
    {
        _startedAt = DateTime.UtcNow;
        ElapsedText = "0:00";
        _elapsedTimer.Start();
    }
    else if (!IsActive)
    {
        _elapsedTimer.Stop();
        ElapsedText = "0:00";
        _preview = string.Empty;
        TranscriptPreview = string.Empty;
    }
}
```

In `HudWindow.axaml`, a "Loading model…" text block bound to `IsLoadingModel` replaces the waveform during that state. The REC dot can display as amber/yellow instead of red to visually distinguish loading from recording.

### manifest.json Entry Template (SHA-256 TBD)

```json
"ggml-medium-he": {
  "sha256": "TODO_COMPUTE_VIA_shasum_-a_256_ggml-hebrew.bin",
  "size_bytes": 1533763059,
  "url": "https://huggingface.co/imvladikon/whisper-medium-he/resolve/241e89a758b512be3e5bae44dc93c2dc34873b81/ggml-hebrew.bin"
}
```

Note: The pinned commit hash `241e89a758b512be3e5bae44dc93c2dc34873b81` was observed in the `x-repo-commit` response header from the HF CDN on 2026-05-24. [VERIFIED: HTTP 302 response from HF] Use this pinned URL format (matching the existing manifest entries) rather than `resolve/main/`.

---

## Runtime State Inventory

> Greenfield feature addition to existing data. No rename or migration of existing runtime data.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | `config.json` gains `language_models: {}` on v3→v4 migration. Old v3 configs load fine — missing key defaults to empty dict. | Schema migration (code edit: `MigrateV3ToV4`) |
| Live service config | None — no external service configurations reference per-language model selection | None |
| OS-registered state | None | None |
| Secrets/env vars | None — no new secrets | None |
| Build artifacts | `models/manifest.json` gains a new entry for `ggml-medium-he`; existing entries unchanged | Add entry (build artifact edit) + SHA-256 computation step |

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single global model for all languages | Per-language model map with lazy swap | Phase 8 | Hebrew users get significantly better accuracy without manual model switching |
| No fine-tuned catalog entries | `ModelCatalog.LanguageModels` with curated language-specific entries | Phase 8 | Extensible pattern for adding more language fine-tunes later |

**Notable:** The Hebrew model `ggml-medium-he` from `imvladikon/whisper-medium-he` is a community ggml conversion. [ASSUMED] It may not exist for all future Whisper versions. The manifest.json SHA-256 pinning strategy (already established) protects against integrity issues.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `Dictionary<string, string>` on a source-gen registered type (`AppConfig`) will be inferred by the STJ source generator without needing an explicit `[JsonSerializable(typeof(Dictionary<string, string>))]` attribute — based on the fact that `Dictionary<string, ModelManifestEntry>` already works this way on `ModelManifest` | Standard Stack / Code Examples | Low — mitigated by adding the explicit `[JsonSerializable]` attribute defensively anyway |
| A2 | The Hebrew model `imvladikon/whisper-medium-he` is significantly better than `ggml-medium` for Hebrew speech dictation (user claim). Research did not include a benchmark run. | Architecture Patterns (Hebrew entry) | Low for this phase — the catalog entry is addable regardless; user bears the accuracy expectation |
| A3 | `WaitAsync(ct)` on the `Task.Run(() => EnsureLoaded(...))` correctly unblocks the caller on cancellation while letting the background load complete | Common Pitfalls (Pitfall 4) | Medium — if the behavior differs from expectation, the model load may hold a lock and the next dictation start will block on `EnsureLoaded`'s `lock (_lock)` until the previous load finishes. Behavior is still correct but adds latency. |

---

## Open Questions

1. **SHA-256 for ggml-medium-he**
   - What we know: The file URL is confirmed reachable (`HTTP 302` to S3 presigned URL); file size confirmed at `1,533,763,059 bytes`.
   - What's unclear: The exact SHA-256 hash. Cannot be computed without downloading the ~1.5 GB file.
   - Recommendation: Plan 08-02 must include an explicit task: "Download `ggml-hebrew.bin`, compute SHA-256 (`shasum -a 256`), add entry to `models/manifest.json`." This is a blocking step before the download path can be tested.

2. **`DictationState.LoadingModel` vs. reusing `DictationState.Processing`**
   - What we know: `DictationState.Processing` is currently used only after hotkey release (transcribing captured audio). `HudWindow.OnStateChanged` handles `Processing` state identically to non-active states (HUD hides).
   - What's unclear: Whether the HUD should be visible during `LoadingModel` (D-10 says yes). Using `Processing` would reuse the existing state but change its meaning.
   - Recommendation: Add `LoadingModel` as a new enum value. This is explicit, avoids semantic collision, and `HudViewModel.OnStateChanged` only needs one new case. The HUD stays visible (spinner / amber dot) while loading.

3. **Avalonia Popup vs. Window for the flyout**
   - What we know: The codebase has zero existing `Popup` or `FlyoutBase` usage. The only dialog pattern is `BlockerDialogWindow` — a plain `Window` shown via code-behind. The `SettingsViewModel` does not hold a `Window` reference directly.
   - What's unclear: Whether `Window.ShowDialog(owner)` works cleanly when called from a `RelayCommand` that runs on the UI thread inside a `Window` that itself opened from a tray icon.
   - Recommendation: Use `Window.ShowDialog`. Pass the `MainWindow` reference into `SettingsViewModel` via constructor or retrieve it from `Application.Current.ApplicationLifetime` at command time. This mirrors the `BlockerDialogWindow` pattern exactly.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `dotnet` SDK | Build + tests | ✓ (via global.json pin) | net10.0 | — |
| `shasum` CLI | SHA-256 computation for manifest.json | ✓ (macOS built-in) | — | `Get-FileHash` on Windows |
| HuggingFace CDN (imvladikon/whisper-medium-he) | Hebrew model download + SHA-256 | ✓ (confirmed HTTP 302 reachable, 2026-05-24) | — | None — required for Hebrew model support |

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 + Shouldly 4.3.0 |
| Config file | none (convention-based) |
| Quick run command | `dotnet test tests/QuickSType.Core.Tests --filter "Category!=Slow"` |
| Full suite command | `dotnet test tests/QuickSType.Core.Tests` |

### Phase Requirements → Test Map

| Plan | Behavior | Test Type | Automated Command | File Exists? |
|------|----------|-----------|-------------------|-------------|
| 08-01 | `language_models` field defaults to empty dict | unit | `dotnet test --filter "LanguageModels"` | ❌ Wave 0 |
| 08-01 | `language_models` round-trips through source-gen | unit | `dotnet test --filter "LanguageModels"` | ❌ Wave 0 |
| 08-01 | v3 JSON without `language_models` key deserializes with empty dict (not null) | unit | `dotnet test --filter "MigrateV3ToV4"` | ❌ Wave 0 |
| 08-01 | `MigrateV3ToV4` bumps SchemaVersion to 4, injects empty dict | unit | `dotnet test --filter "MigrateV3ToV4"` | ❌ Wave 0 |
| 08-01 | `WithLanguageModel` helper sets and clears per-language assignment | unit | `dotnet test --filter "WithLanguageModel"` | ❌ Wave 0 |
| 08-02 | Every catalog model (including `ggml-medium-he`) has a manifest entry | unit | `dotnet test --filter "Every_catalog_model"` | ✅ (existing, will start failing until manifest.json is updated) |
| 08-02 | `ModelCatalog.LanguageModels["he"]` contains `ggml-medium-he` | unit | `dotnet test --filter "LanguageModels_he"` | ❌ Wave 0 |
| 08-03 | `ResolveModelId` returns language-specific model when assigned | unit | `dotnet test --filter "ResolveModelId"` | ❌ Wave 0 |
| 08-03 | `ResolveModelId` returns global model when `AutoLanguage=true` | unit | `dotnet test --filter "ResolveModelId"` | ❌ Wave 0 |
| 08-03 | `ResolveModelId` falls back to global when no language-specific assignment | unit | `dotnet test --filter "ResolveModelId"` | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test tests/QuickSType.Core.Tests --filter "LanguageModels|MigrateV3ToV4|WithLanguageModel|ResolveModelId|LanguageModels_he"`
- **Per wave merge:** `dotnet test tests/QuickSType.Core.Tests`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps

- [ ] `tests/QuickSType.Core.Tests/ConfigRoundTripV4Tests.cs` — covers `language_models` round-trip, default empty dict, null safety, snake_case JSON key
- [ ] `tests/QuickSType.Core.Tests/ConfigMigrationV4Tests.cs` — covers `MigrateV3ToV4` bumps version, injects empty dict, preserves all v3 fields
- [ ] `tests/QuickSType.Core.Tests/ModelCatalogLanguageTests.cs` — covers `LanguageModels["he"]` contains `ggml-medium-he`, Arabic not in map, `Find("ggml-medium-he")` returns non-null
- [ ] `tests/QuickSType.Core.Tests/DictationEngineModelResolutionTests.cs` — covers `ResolveModelId` for all three cases (assigned, auto-language, fallback)

---

## Security Domain

> `security_enforcement: true` (from config.json). ASVS level 1.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | yes — language code + model ID from config | Validate model ID via `ModelCatalog.Find(modelId)` before using as a file path; prevents path traversal via config manipulation |
| V6 Cryptography | yes — SHA-256 model integrity | Already handled by `ModelDownloader`; Hebrew model entry must have correct SHA-256 in manifest.json |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via crafted `language_models` value (e.g., `"../../evil.bin"`) | Tampering | `ResolveModelId` must validate model ID via `ModelCatalog.Find(modelId)` — if `Find` returns null, fall back to global model; never use raw dict value as a path without validation |
| SHA-256 bypass for Hebrew model | Tampering | `ModelDownloader.DownloadAsync` always verifies SHA-256 from manifest — no bypass possible as long as manifest.json has the correct hash and the verification path is exercised |
| Null `LanguageModels` causing NRE | Denial of Service | Property initializer `= new()` + migration path ensures non-null; defensive null-check in `ResolveModelId` |

---

## Sources

### Primary (HIGH confidence)

- Codebase direct read: `src/QuickSType.Core/Config/Config.cs` — current `AppConfig` record, all fields, mutation helpers, SchemaVersion=3
- Codebase direct read: `src/QuickSType.Core/Config/ConfigStore.cs` — migration pattern (`MigrateV2ToV3`), `Load()` migration guard
- Codebase direct read: `src/QuickSType.Core/Config/JsonContext.cs` — `ConfigJsonContext` registrations; absence of explicit `Dictionary<...>` registration on `ModelManifest` confirms implicit inference works
- Codebase direct read: `src/QuickSType.Core/Transcribe/ModelCatalog.cs` — current `ModelInfo` shape, `All` list, `Find()`, `PathFor()`, `IsInstalled()`
- Codebase direct read: `src/QuickSType.Core/Transcribe/Transcriber.cs` — `EnsureLoaded` signature, `_loadedModelPath` check, `LoadedModelPath` property
- Codebase direct read: `src/QuickSType.Core/DictationEngine.cs` — `DictationState` enum (Idle/Recording/Streaming/Processing), `ProcessAsync` model resolution at line 312, `_cts` cancellation flow
- Codebase direct read: `src/QuickSType.Core/Transcribe/ModelManifest.cs` — `ModelManifestLoader`, SHA-256 verification path, `GetSha256` throwing `ArgumentException` on missing ID
- Codebase direct read: `models/manifest.json` — existing entries, URL format (pinned commit hash)
- Codebase direct read: `src/QuickSType.UI/ViewModels/SettingsViewModel.cs` — `LanguageRow` shape, `AvailableModels` construction from `ModelCatalog.All`, `DownloadSelectedModelCommand` progress pattern
- Codebase direct read: `src/QuickSType.UI/Views/MainWindow.axaml` — Languages tab `ItemsControl` with `LanguageRow` `DataTemplate`
- Codebase direct read: `src/QuickSType.UI/ViewModels/HudViewModel.cs` — `OnStateChanged` dispatch table, `IsRecording`/`IsStreaming` boolean properties
- Codebase direct read: `src/QuickSType.UI/Views/BlockerDialogWindow.axaml.cs` — `Window.ShowDialog` pattern (only modal dialog in codebase)
- HTTP HEAD confirmation: `curl -sI https://huggingface.co/imvladikon/whisper-medium-he/resolve/main/ggml-hebrew.bin` → `HTTP/2 302`, `x-linked-size: 1533763059`, `x-repo-commit: 241e89a758b512be3e5bae44dc93c2dc34873b81` [VERIFIED: live HTTP probe, 2026-05-24]
- Codebase direct read: `tests/QuickSType.Core.Tests/ConfigMigrationV3Tests.cs` + `ConfigRoundTripV3Tests.cs` — test shape for migration and round-trip tests (Wave 0 test files for Phase 8 should mirror this shape)
- Codebase direct read: `tests/QuickSType.Core.Tests/ModelManifestTests.cs` — `Every_catalog_model_has_a_manifest_entry` test confirms manifest completeness enforcement

### Secondary (MEDIUM confidence)

- [ASSUMED] STJ source-gen behavior for `Dictionary<string, string>` inferred from existing `Dictionary<string, ModelManifestEntry>` precedent — behavior consistent with .NET 7+ STJ source-gen documentation

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — no new packages; all existing libraries confirmed via codebase reads
- Architecture: HIGH — confirmed via direct file reads of all integration points
- Pitfalls: HIGH — derived from actual codebase behavior (existing tests, existing manifest loading code)
- Hebrew model URL: HIGH — confirmed via live HTTP probe
- Hebrew model SHA-256: UNKNOWN — requires downloading 1.5 GB file; must be computed in Plan 08-02

**Research date:** 2026-05-24
**Valid until:** 2026-06-24 (stable stack; Hebrew model URL pinned to commit hash for resilience)
