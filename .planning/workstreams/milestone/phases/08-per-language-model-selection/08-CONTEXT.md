# Phase 8: Per-Language Model Selection - Context

**Gathered:** 2026-05-24
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can assign a dedicated Whisper model per active language. Arabic and Hebrew gain language-specific curated fine-tuned catalog entries (Hebrew: `ggml-medium-he` from `imvladikon/whisper-medium-he`; Arabic: no ggml fine-tune available yet — defaults to the global model). Config gains a `language_models` map (`Dictionary<string, string>`, language code → model ID). DictationEngine resolves the correct model per active language before transcribing and loads it lazily at dictation start. Falls back to the global `Model` field if no language-specific model is assigned. Per-language model picker surfaces as a gear-button flyout per language row in the Settings Languages section.

This phase does not change streaming/VAD logic, auto-update, crash telemetry, or distribution packaging.

</domain>

<decisions>
## Implementation Decisions

### Settings UI

- **D-01:** Per-language model picker lives as a gear/cog button on each enabled language row in the Languages tab. Clicking the gear opens a modal/flyout (implementation — Popup or Dialog — left to Claude's discretion based on Avalonia patterns already in the codebase).
- **D-02:** Language rows do NOT show the currently-assigned model name inline — the gear icon is the only affordance. Model detail is shown inside the flyout only.
- **D-03:** The existing global model picker (Models tab) stays clean — it lists only the standard global models (tiny/base/small/medium/large/large-v3-turbo/large-v3-turbo-q5). Fine-tuned language-specific models appear only in the per-language flyout, never in the global list.
- **D-04:** The per-language flyout includes a download button + progress bar inline (reusing the existing `ModelDownloader` + progress pattern) for models that haven't been downloaded yet. No separate navigation required to download a language-specific model.

### Model Catalog

- **D-05:** Hebrew fine-tuned model is added as a named entry in `ModelCatalog.All` (ID: `ggml-medium-he`, URL: `https://huggingface.co/imvladikon/whisper-medium-he/resolve/main/ggml-hebrew.bin`). It is tagged to the `"he"` language so it only appears in the Hebrew flyout.
- **D-06:** Arabic has no curated ggml fine-tune available. Arabic entry in the per-language flyout shows only the global models (re-exposed as choices). No placeholder or broken entry — just "Use global model" as the only option until a fine-tuned Arabic ggml is available.
- **D-07:** `ModelCatalog` gains a `LanguageModels` dictionary: `Dictionary<string, IReadOnlyList<ModelInfo>>` mapping language codes to their curated fine-tuned entries. Global models are NOT in this map — they are accessible from `ModelCatalog.All`. The flyout shows language-specific models first, then offers the global model as the fallback choice.
- **D-08:** `AppConfig` gains a `language_models` field: `Dictionary<string, string>` (snake_case JSON key), serialized via `ConfigJsonContext` source-gen. Default is an empty dictionary (no overrides). SchemaVersion bumps from `3` → `4`. Migration: v3 configs load cleanly — empty `language_models` means "use global for all languages."

### Model Swap Behavior

- **D-09:** Model loading is **lazy**: DictationEngine resolves the active language's assigned model just before `EnsureLoaded()`. If it differs from the currently loaded model, it reloads at that point. No background pre-warm.
- **D-10:** While the model is loading at dictation start, the HUD shows a "Loading model…" state. Recording begins only after `EnsureLoaded()` completes. The user holds the hotkey; the HUD signals readiness.
- **D-11:** If the user releases the hotkey while the model is still loading, the load is cancelled (via `CancellationToken`), the engine returns to `Idle`, and nothing is transcribed. Consistent with existing short-press / early-release behavior.

### Auto-Language Mode

- **D-12:** When `AutoLanguage=true`, the per-language model map does NOT apply. DictationEngine uses the global model and lets Whisper detect the spoken language freely. No mid-session model swaps based on detected language. Per-language model map is only consulted when a specific language is manually selected as `ActiveLanguage`.

### Claude's Discretion

- Whether to implement the flyout as an Avalonia `Popup` (anchored to the gear button within the language row) or as a lightweight `Dialog` window — pick whichever integrates most cleanly with existing window/dialog patterns in the codebase.
- Thread management for wrapping `Transcriber.EnsureLoaded()` (sync, locked) in `Task.Run` for the lazy-load path — don't block the UI thread.
- Whether to add a new `DictationEngine` state variant for "loading model" or reuse an existing transitional state.
- Exact `ConfigStore.MigrateV3ToV4` logic (v3 → v4 migration is straightforward: inject empty `language_models: {}` and bump version).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & Requirements
- `.planning/workstreams/milestone/ROADMAP.md` — Phase 8 goal, plan breakdown, cross-cutting constraints
- `.planning/PROJECT.md` — Key decisions, constraints (AOT, async, binary size)
- `.planning/workstreams/milestone/STATE.md` — Current project status

### Config & Schema
- `src/QuickSType.Core/Config/Config.cs` — `AppConfig` record; add `language_models` field here
- `src/QuickSType.Core/Config/ConfigStore.cs` — Migration logic; add `MigrateV3ToV4`
- `src/QuickSType.Core/Config/JsonContext.cs` — `ConfigJsonContext` source-gen; register new Dictionary type

### Model Catalog
- `src/QuickSType.Core/Transcribe/ModelCatalog.cs` — Add `ggml-medium-he` entry and `LanguageModels` map
- `src/QuickSType.Core/Transcribe/Transcriber.cs` — `EnsureLoaded()` swap-on-demand path; wrap in `Task.Run` for async

### DictationEngine & Pipeline
- `src/QuickSType.Core/DictationEngine.cs` — Model resolution logic; add "Loading model" HUD state
- `src/QuickSType.Core/Transcribe/TranscriptUpdate.cs` — Existing streaming types

### Settings UI
- `src/QuickSType.UI/ViewModels/SettingsViewModel.cs` — Extend `LanguageRow` with per-language model assignment
- `src/QuickSType.UI/Views/MainWindow.axaml` — Add gear button + flyout to language rows
- `src/QuickSType.UI/Composition/AppHost.cs` — Wiring for any new services

### Prior Phase Context (for patterns)
- `.planning/workstreams/milestone/phases/04-streaming-engine-vad/04-CONTEXT.md` — DictationEngine state machine decisions
- `.planning/workstreams/milestone/phases/05-hud-injection-polish/05-CONTEXT.md` — HUD state + IIncrementalInjector decisions
- `.planning/workstreams/milestone/phases/02-config-model-picker/02-CONTEXT.md` (if exists) — Config schema v3 decisions

### Hebrew Fine-tuned Model (external)
- HF repo: `imvladikon/whisper-medium-he` — Pre-converted ggml binary. URL: `https://huggingface.co/imvladikon/whisper-medium-he/resolve/main/ggml-hebrew.bin`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Transcriber.EnsureLoaded(modelPath, useGpu)`: Already checks `_loadedModelPath == modelPath` — no reload if unchanged. Phase 8 calls this for per-language models; only swaps when the resolved model ID differs.
- `ModelDownloader.DownloadAsync(ModelInfo, IProgress<Progress>?, CancellationToken)`: Reuse for downloading language-specific fine-tuned models in the per-language flyout.
- `ModelRowViewModel`: Existing VM for model rows in the global picker. May be reusable or adaptable for the per-language flyout model list.
- `LanguageRow` (in `SettingsViewModel`): Existing row VM for each language. Phase 8 extends it with a selected model ID property and a command to open the per-language flyout.

### Established Patterns
- AOT-safe serialization: All new config fields go through `ConfigJsonContext` source-gen. `Dictionary<string, string>` will need `[JsonSerializable(typeof(Dictionary<string, string>))]` added to the context.
- Record `with` mutations: AppConfig uses `with` expressions for all mutations — add a `WithLanguageModel(string lang, string modelId)` helper following the same pattern as `WithPreferredModel`.
- Optional `ILogger<T>?` with `NullLogger.Instance` fallback: All services follow this; any new service added must follow it.
- `CancellationToken` threading: The lazy-load path must propagate the existing `_cts.Token` from DictationEngine into the model load call.

### Integration Points
- `DictationEngine.ProcessAsync` is where model resolution and `Transcriber.EnsureLoaded()` must be called with the language-resolved model path (not just `config.Model`).
- `TrayService.OnConfigChanged` may need to respond to `language_models` changes if any tray display is needed (likely not — per-language model is a silent behavior change).
- `HudViewModel`/`HudWindow`: Needs to handle a new "Loading" state for the model-load delay (D-10). This connects to `DictationEngine.StateChanged`.

</code_context>

<specifics>
## Specific Ideas

- Hebrew fine-tuned model: `imvladikon/whisper-medium-he` / `ggml-medium-he` / `https://huggingface.co/imvladikon/whisper-medium-he/resolve/main/ggml-hebrew.bin` — this is the specific model the user intends for Hebrew.
- Arabic: No ggml fine-tune available yet. Arabic per-language flyout shows global models only (no broken placeholder).
- HUD "Loading model…" state: Should be visually distinct from the normal "Recording" state — consider a spinner or different color on the REC dot while loading.

</specifics>

<deferred>
## Deferred Ideas

- **Dynamic HF model browser**: User raised the idea of a dynamic per-language HuggingFace API query to discover community-converted models. Deferred — adds runtime network complexity and HF API coupling. V1 uses curated static catalog only.
- **Arabic fine-tuned ggml model**: No pre-built ggml Arabic fine-tune currently available in the community. When one becomes available (or if the user converts one), it can be added to `ModelCatalog.LanguageModels["ar"]` without any schema change.
- **Auto-language → model-swap**: User asked about detected-language driving model selection when AutoLanguage=true. Deferred — adds mid-session model swap complexity; deferred to a future iteration.
- **More languages beyond Arabic/Hebrew**: Expanding the per-language fine-tune catalog to other languages (e.g., Spanish, French) is intentionally left open by the `Dictionary<string, IReadOnlyList<ModelInfo>>` structure but is not scoped to Phase 8.

</deferred>

---

*Phase: 8-per-language-model-selection*
*Context gathered: 2026-05-24*
