---
phase: 02-config-model-picker
plan: 07
subsystem: settings-ui
tags: [csharp, dotnet, avalonia, xaml, mvvm, ui, first-run, hardware-warnings]
requires:
  - phase: 02-config-model-picker
    plan: 04
    provides: ISystemSpecsService port, SystemSpecsService BCL impl, HardwareTier
  - phase: 02-config-model-picker
    plan: 05
    provides: AppHost.SystemSpecsService, AppHost.SystemSpecs, LoggerFactory
  - phase: 02-config-model-picker
    plan: 06
    provides: MainWindow.axaml Models tab scaffold (ComboBox, buttons, ProgressBar)
provides:
  - ModelRowViewModel wrapper VM (ModelInfo + HardwareWarning)
  - IsFirstRun observable property driven by PreferredModel == null (D-01)
  - First-run banner (S2) in Models tab with auto-selected recommended model
  - Per-model hardware warning annotations (S3) in ComboBox items
  - PreferredModel persisted after download success + SHA-256 verify (D-03)
  - Banner does not re-appear on subsequent launches (PreferredModel != null)
affects: []
requirements-completed: [CONFIG-02, CONFIG-04]
tech-stack:
  added: []
  patterns:
    - "ModelRowViewModel wrapper for ComboBox items (mirrors LanguageRow pattern)"
    - "IsFirstRun driven by PreferredModel == null sentinel (D-01)"
    - "PreferredModel persisted after DownloadAsync returns without throwing (D-03)"
    - "SaveModel keeps PreferredModel in sync with Model (D-02)"
    - "Warned models stay selectable (D-05) — warning is inline text, not a block"
key-files:
  created:
    - src/QuickSType.UI/ViewModels/ModelRowViewModel.cs
  modified:
    - src/QuickSType.UI/ViewModels/SettingsViewModel.cs (+90/-6)
    - src/QuickSType.UI/Views/MainWindow.axaml (+20/-4)
key-decisions:
  - "D-01: PreferredModel == null sets IsFirstRun = true; banner visible"
  - "D-02: SaveModel sets both Model and PreferredModel in sync"
  - "D-03: PreferredModel written after DownloadAsync verify pass, not at click time"
  - "D-05: HardwareWarning is inline text (not a block); models stay selectable"
metrics:
  duration: 12min
  completed: 2026-05-09
---

# Phase 02: Config & Model Picker -- Plan 07 Summary

**First-run banner (S2) + per-model hardware warnings (S3) in Settings Models tab -- ModelRowViewModel wrapper, PreferredModel persistence after download verify, 127 tests GREEN, 0 build errors**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-09T22:00:00Z
- **Completed:** 2026-05-09T22:12:00Z
- **Tasks:** 2 of 3 (checkpoint task halted for manual verification)
- **Files created:** 1
- **Files modified:** 2

## Accomplishments

### Task 1: ModelRowViewModel + SettingsViewModel first-run state and PreferredModel persistence

- Created `ModelRowViewModel.cs` -- wrapper VM exposing `ModelInfo` and `HardwareWarning` (string?). Mirrors existing `LanguageRow` pattern. Constructor takes `ModelInfo`, `SystemSpecs`, `ISystemSpecsService` and calls `GetHardwareWarning` to precompute the warning string.
- Extended `SettingsViewModel` with 3 new `[ObservableProperty]` fields:
  - `_isFirstRun` (bool) -- banner visibility
  - `_firstRunBannerBody` (string) -- computed banner text with hardware recommendation
  - `_firstRunBannerCaption` (string) -- static "You can change this any time in Settings."
- Added `ILogger _log` field for structured logging.
- Changed `AvailableModels` from `ObservableCollection<ModelInfo>` to `ObservableCollection<ModelRowViewModel>`.
- Constructor now handles first-run model selection (D-01):
  - When `PreferredModel is null`: calls `RecommendModel`, sets `SelectedModel` to recommended ID, computes `sizeMb` (rounded to nearest 10 MB), maps `HardwareTier` to label string, constructs banner body, logs "First run: recommending {ModelId} for {Tier}".
  - When `PreferredModel is not null`: uses `PreferredModel` as `SelectedModel`, sets `IsFirstRun = false`.
- `DownloadSelectedModel` command updated (D-02, D-03):
  - Status text shows `Starting download...` then `Downloading... X% (... MB, ... MB/s)` during progress.
  - After `DownloadAsync` succeeds: shows `Verifying SHA-256...`, calls `_host.UpdateConfig(_host.Config.WithPreferredModel(SelectedModel) with { Model = SelectedModel })`, clears `IsFirstRun` if active, logs "PreferredModel set to {ModelId}; first-run flow complete".
  - Status auto-clears after 5 seconds via `AutoClearStatusAsync`.
- `SaveModel` command updated (D-02): now sets both `Model = SelectedModel, PreferredModel = SelectedModel` in the `with` expression.
- **Rule 1 fix**: Reverted local-only `MinRamGb` value from `1112.0` to `2.0` in `SystemSpecsService.cs` -- a pre-existing local modification from a prior testing session that would break the blocker dialog (every machine would fail the min-RAM check).

### Task 2: MainWindow.axaml first-run banner and hardware warning row

- Added first-run banner `Border` above the `ComboBox` in the Models tab (UI-SPEC S2):
  - `Background="{DynamicResource SystemAltLowColor}"`, `CornerRadius="6"`, `Padding="16,12"`, `IsVisible="{Binding IsFirstRun}"`
  - Contains `h1` heading "Pick a model to get started", body bound to `FirstRunBannerBody`, muted caption bound to `FirstRunBannerCaption`.
- Retargeted `ComboBox.ItemTemplate` DataType from `transcribe:ModelInfo` to `vm:ModelRowViewModel` -- fixes Pitfall 5 (type mismatch).
- Updated binding paths: `Model.DisplayName`, `Model.Description`, `Model.Id` (SelectedValueBinding traversal).
- Added third `TextBlock` for hardware warning (UI-SPEC S3): `Foreground="#D9822B"`, `FontSize="12"`, `IsVisible` via `StringConverters.IsNotNullOrEmpty`.

### Task 3: Manual verification (checkpoint:human-verify)

**Status: AWAITING VERIFICATION.** See checkpoint message below for detailed verification steps.

## Test Status

| Test Suite | Tests | Status |
|---|---|---|
| Full Phase-2 Core suite | 127 | 127/127 GREEN (0 failed, 0 skipped) |
| Solution build | -- | 0 errors (2 pre-existing NU1903 warnings) |

## Decision IDs Implemented

- **D-01** (PreferredModel == null sentinel): Implemented. Constructor checks `c.PreferredModel is null` and sets `_isFirstRun = true`, calls `RecommendModel`, auto-selects the recommended model in the ComboBox.
- **D-02** (PreferredModel always in sync with Model): Implemented. Both `SaveModel` and the success path of `DownloadSelectedModel` write `PreferredModel = SelectedModel` alongside `Model = SelectedModel`.
- **D-03** (PreferredModel written after verify): Implemented. `_host.UpdateConfig(...)` with `WithPreferredModel(SelectedModel)` is called AFTER `await _host.ModelDownloader.DownloadAsync(...)` returns (verify passes without exception).
- **D-05** (Warn don't block): Implemented. `HardwareWarning` is inline text in the ComboBox item template. No model is hidden or disabled. Warning is advisory text-only with amber `#D9822B` foreground.

## Telemetry / Logging Hooks

| Event | Location | Status |
|-------|----------|--------|
| `First run: recommending {ModelId} for {Tier}` | SettingsViewModel constructor | Landed |
| `PreferredModel set to {ModelId}; first-run flow complete` | DownloadSelectedModel success path | Landed |

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ModelRowViewModel, add first-run state and PreferredModel persistence to SettingsViewModel** - `e3feb5e` (feat, 2 files, +90/-6)
2. **Task 2: Add first-run banner and hardware warning row to Models tab** - `1a888b9` (feat, 1 file, +20/-4)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Reverted MinRamGb from 1112.0 to 2.0**
- **Found during:** Task 1 test verification (2 SystemSpecsTests failures)
- **Issue:** `SystemSpecsService.MinRamGb` was set to `1112.0` as a local-only working-tree modification from a prior session. This would make the hardware blocker dialog trigger on every machine (no machine has 1112 GB RAM).
- **Fix:** Reverted to the committed value of `2.0`.
- **Files modified:** `src/QuickSType.Core/Platform/SystemSpecsService.cs`
- **Commit:** (included in Task 1 commit `e3feb5e`)

### Pre-existing Issues Discovered

- The 2 failing tests (`IsBlocker_false_for_2GB_RAM_and_1GB_disk_boundary`, `IsBlocker_false_for_typical_modern_machine`) were caused by a local-only (uncommitted) `MinRamGb = 1112.0` modification. Reverting it restored all 127 tests to GREEN.

## Phase 2 Functional Completion

This plan completes the Phase 2 UI surfaces. All five CONFIG requirements are now live:

- **CONFIG-01**: schema v2->v3 migration (Plans 01, 02)
- **CONFIG-02**: hardware-recommended first-run model + per-row warnings (Plans 04, 07)
- **CONFIG-03**: pre-tray blocker dialog (Plans 04, 06)
- **CONFIG-04**: PreferredModel override persists; banner does not re-appear (Plan 07)
- **CONFIG-05**: JSONL history schema + write-on-paste (Plans 03, 05)

## Next Phase Readiness

- `host.SystemSpecsService` and `host.SystemSpecs` are now globally available on `AppHost`.
- `SettingsViewModel` has first-run state, download-verify-persist flow, and hardware warnings.
- Phase 3 PLAT-* services should reuse the same `host.*` injection pattern.

## AOT Safety

No new reflection, P/Invoke, or dynamic code introduced:
- `ModelRowViewModel` is a simple DTO with no AOT-relevant code.
- All new bindings use compiled bindings via `x:DataType` declarations.
- `StringConverters.IsNotNullOrEmpty` is a built-in Avalonia static converter (no reflection).

## Self-Check: PASSED

- FOUND: Commit `e3feb5e` (feat(02-07): create ModelRowViewModel + SettingsViewModel first-run state)
- FOUND: Commit `1a888b9` (feat(02-07): add first-run banner and hardware warning row)
- FOUND: `src/QuickSType.UI/ViewModels/ModelRowViewModel.cs` exists
- PASSED: grep checks for all 22 acceptance-criteria patterns in SettingsViewModel.cs
- PASSED: grep checks for all 12 acceptance-criteria patterns in MainWindow.axaml
- PASSED: `dotnet build QuickSType.sln` -- 0 errors
- PASSED: `dotnet test tests/QuickSType.Core.Tests/` -- 127/127 GREEN

---

*Phase: 02-config-model-picker*
*Completed: 2026-05-09*
