---
phase: 08-per-language-model-selection
plan: "03"
subsystem: dictation-engine
tags: [per-language-model, whisper, cancellation, hud, avalonia, mvvm]

requires:
  - phase: 08-01
    provides: AppConfig.LanguageModels dictionary + WithLanguageModel helper
  - phase: 08-02
    provides: ModelCatalog.LanguageModels dict, ggml-medium-he entry with LanguageCode="he"

provides:
  - DictationEngine.ResolveModelId(AppConfig) static helper with D-12 AutoLanguage bypass and T-08-04 catalog validation guard
  - DictationState.LoadingModel enum value (between Idle and Recording)
  - Lazy per-language model EnsureLoaded via Task.Run(...).WaitAsync(ct) in ProcessAsync
  - D-11 cancellation path: hotkey release during LoadingModel cancels via _cts.Cancel()
  - HudViewModel.IsLoadingModel observable property
  - HudViewModel.IsActive extended to include DictationState.LoadingModel

affects:
  - 08-04
  - hud-view

tech-stack:
  added: []
  patterns:
    - "Task.Run(...).WaitAsync(ct) for cancellable sync work (never Task.Run(..., ct))"
    - "DictationState with pre-recording transitional LoadingModel state"
    - "Static internal helper on DictationEngine for testability without full graph construction"

key-files:
  created:
    - tests/QuickSType.Core.Tests/DictationEngineModelResolutionTests.cs
  modified:
    - src/QuickSType.Core/DictationEngine.cs
    - src/QuickSType.UI/ViewModels/HudViewModel.cs

key-decisions:
  - "ResolveModelId made static internal so tests can call it without constructing a full DictationEngine with real services"
  - "T-08-04 catalog validation guard in ResolveModelId prevents arbitrary path injection via ModelCatalog.PathFor"
  - "D-11 structural test (Approach B) chosen over behavioral test due to DictationEngine requiring heavyweight native services (audio, Whisper)"
  - "WaitAsync(ct) anti-pattern test embedded in structural assertion to pin the correct cancellation idiom"

patterns-established:
  - "LoadingModel as a pre-recording transitional state: HUD stays active, elapsed timer starts, hotkey release cancels cleanly"

requirements-completed: []

duration: 7min
completed: 2026-05-24
---

# Phase 08 Plan 03: DictationEngine Model Resolution Summary

**Per-language model resolution wired into DictationEngine via static ResolveModelId, lazy EnsureLoaded with Task.Run(...).WaitAsync(ct), LoadingModel state, and HudViewModel.IsLoadingModel observable**

## Performance

- **Duration:** 7 min
- **Started:** 2026-05-24T10:25:01Z
- **Completed:** 2026-05-24T10:32:20Z
- **Tasks:** 2
- **Files modified:** 3 + 1 created

## Accomplishments

- `DictationState.LoadingModel` enum value added between `Idle` and `Recording`
- `DictationEngine.ResolveModelId(AppConfig)` static method with full fallback chain: D-12 AutoLanguage bypass, T-08-04 catalog validation guard (prevents stale-config path injection), empty string guard
- `ProcessAsync` updated to call `ResolveModelId` at dictation start and lazy-load via `Task.Run(() => _transcriber.EnsureLoaded(modelPath, useGpu)).WaitAsync(ct)` only when model path changes
- `OnHotkeyReleased` guard for `LoadingModel` state calls `_cts?.Cancel()` and returns (D-11 path)
- `HudViewModel.IsLoadingModel` observable + `IsActive` extended to include `LoadingModel` so HUD stays visible and elapsed timer starts during model load
- 7 xUnit tests pinning all ResolveModelId fallback paths + D-11 structural invariant

## Task Commits

1. **RED - DictationEngineModelResolutionTests (failing)** - `dcef92c` (test)
2. **GREEN - DictationEngine LoadingModel + ResolveModelId** - `5c20cc6` (feat)
3. **Task 2 - HudViewModel IsLoadingModel** - `1bdc234` (feat)

**Prerequisite merge:** `79fe985` (chore: merge 08-01 + 08-02 into worktree)

## Files Created/Modified

- `src/QuickSType.Core/DictationEngine.cs` - Added LoadingModel enum, static ResolveModelId, lazy EnsureLoaded in ProcessAsync, OnHotkeyReleased guard
- `src/QuickSType.UI/ViewModels/HudViewModel.cs` - Added IsLoadingModel observable, extended IsActive and OnStateChanged
- `tests/QuickSType.Core.Tests/DictationEngineModelResolutionTests.cs` - 7 tests: 6 ResolveModelId fallback cases + 1 D-11 structural test

## Decisions Made

- **ResolveModelId is static internal:** Plan explicitly allowed refactoring to static if constructing DictationEngine in tests was impractical — it is, because DictationEngine requires real audio hardware and Whisper native runtime. Static makes it fully testable without any mocking infrastructure.
- **D-11 test uses Approach B (structural):** Behavioral test (Approach A) would require a fake blocking Transcriber and hooking the async task lifecycle, which is brittle given PortAudio native init. Structural grep test provides deterministic verification of the cancellation contract.
- **`.WaitAsync(ct)` assertion pattern:** The structural test looks for `).WaitAsync(ct)` rather than `WaitAsync(ct)` to avoid false-positive match on comments containing the string.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Worktree branch missing Plans 08-01 and 08-02 changes**
- **Found during:** Task 1 setup
- **Issue:** The worktree was branched before 08-01/08-02 were committed on `gsd/phase-08-per-language-model-selection`; `AppConfig.LanguageModels` and `ModelCatalog.LanguageModels` were absent
- **Fix:** Merged `main` (to get recent research/AGENTS.md), then merged `gsd/phase-08-per-language-model-selection` (to get 08-01/08-02 changes); resolved one `models/manifest.json` URL conflict
- **Files modified:** manifest.json, Config.cs, ConfigStore.cs, JsonContext.cs, ModelCatalog.cs, SettingsViewModel.cs + test files
- **Committed in:** `79fe985`

**2. [Rule 1 - Bug] Structural test false-positive on WaitAsync(ct) in comment**
- **Found during:** Task 1 test run (1 test failing)
- **Issue:** `source.IndexOf("WaitAsync(ct)")` found the string first in line 178 comment (`// D-11: hotkey release during model load cancels via WaitAsync(ct) path`) before the actual `SetState(DictationState.LoadingModel)` at line 353 — making `waitAsyncIdx > loadingModelIdx` false
- **Fix:** Changed assertion to `).WaitAsync(ct)` to match only the actual call site, not comments
- **Files modified:** DictationEngineModelResolutionTests.cs
- **Verification:** All 7 tests pass after fix

---

**Total deviations:** 2 auto-fixed (1 blocking setup issue, 1 bug in test assertion)
**Impact on plan:** Both fixes necessary for correct execution. No scope creep.

## Issues Encountered

- Homebrew `dotnet` resolves to .NET 8 (Homebrew install), not .NET 10 (user's `~/.dotnet`); all `dotnet` invocations used `$HOME/.dotnet/dotnet` explicitly.

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes introduced. `ResolveModelId` validates model IDs against `ModelCatalog.Find` before passing to `ModelCatalog.PathFor` — T-08-04 mitigation is implemented and tested by the `ResolveModelId_falls_back_to_global_for_unknown_model_id` test.

## Known Stubs

None — all properties are wired. `IsLoadingModel` visual treatment (amber dot, spinner) is intentionally deferred to Plan 08-04 per the plan spec.

## Next Phase Readiness

- Plan 08-04 can bind `HudViewModel.IsLoadingModel` to visual treatment (amber dot, spinner, "Loading model…" label)
- `DictationEngine.ResolveModelId` is fully tested and ready for production use
- All 249 Core tests pass; UI build exits 0

---
*Phase: 08-per-language-model-selection*
*Completed: 2026-05-24*
