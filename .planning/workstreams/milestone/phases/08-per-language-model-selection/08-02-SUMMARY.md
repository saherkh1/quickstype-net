---
phase: 08-per-language-model-selection
plan: "02"
subsystem: model-catalog
tags: [model-catalog, per-language, manifest, settings-vm, tdd]
dependency_graph:
  requires: []
  provides: [ModelInfo.LanguageCode, ModelCatalog.LanguageModels, manifest-he-sha256, AvailableModels-filter]
  affects: [08-03-dictation-engine-resolution, 08-04-per-language-flyout]
tech_stack:
  added: []
  patterns: [positional-record-extension, OrdinalIgnoreCase-dictionary, Where-filter-in-ObservableCollection]
key_files:
  created:
    - tests/QuickSType.Core.Tests/ModelCatalogLanguageTests.cs
  modified:
    - src/QuickSType.Core/Transcribe/ModelCatalog.cs
    - src/QuickSType.UI/ViewModels/SettingsViewModel.cs
    - models/manifest.json
decisions:
  - "Hebrew model SHA-256 computed from actual downloaded bytes (3d7881b4...) per project_hf_lfs_sha256_gotcha — not copied from HF LFS pointer"
  - "HF commit 241e89a758b512be3e5bae44dc93c2dc34873b81 pinned in both ModelCatalog.cs const HfHe and manifest.json url"
  - "LanguageCode added as optional positional parameter with default null — all existing ModelInfo construction calls compile unchanged"
  - "LanguageModels dict uses StringComparer.OrdinalIgnoreCase so LanguageModels['HE'] and ['he'] both resolve"
  - "Arabic ('ar') absent from LanguageModels per project_arabic_no_ggml_fine_tune decision"
metrics:
  duration: "~5 minutes (including ~4min model download)"
  completed: "2026-05-24T10:19:01Z"
  tasks_completed: 2
  files_changed: 4
---

# Phase 08 Plan 02: Hebrew Model Catalog Entry Summary

Extended ModelCatalog with LanguageCode-tagged ModelInfo, Hebrew ggml-medium-he fine-tune entry, and LanguageModels dictionary; added verified manifest.json SHA-256 computed from actual download; filtered global Settings Models tab to exclude language-specific entries; pinned with 6 new xUnit tests.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Download Hebrew model + compute SHA-256 (auto-executed in YOLO mode) | N/A (data captured) |
| 2 | Add LanguageCode to ModelInfo, Hebrew entry, LanguageModels dict, manifest entry, filter SettingsViewModel, add tests | 8ef8112 |

## Verification Results

- `dotnet build QuickSType.sln` exits 0 (4 pre-existing NU1903 warnings only)
- `dotnet test QuickSType.Core.Tests --filter "ModelCatalogLanguageTests|ModelManifestTests"`: 10/10 passed
- `dotnet test QuickSType.Core.Tests` (full suite): 229/229 passed, 0 failures
- `grep -c '"ggml-medium-he"' models/manifest.json` = 1 ✓
- `grep -c '"ggml-medium-he"' ModelCatalog.cs` = 2 (entry + const comment) ✓
- `grep -c 'TODO_COMPUTE' models/manifest.json` = 0 ✓
- `grep 'LanguageCode is null' SettingsViewModel.cs` = 1 match ✓

## What Was Built

### ModelCatalog.cs changes

- Added `string? LanguageCode = null` as 6th positional parameter to `ModelInfo` record — all existing 7-entry `new(...)` calls compile unchanged (default = null)
- Added `private const string HfHe` pinned to commit `241e89a758b512be3e5bae44dc93c2dc34873b81`
- Appended `ggml-medium-he` entry to `All` with `LanguageCode: "he"`, 1,533,763,059 bytes
- Added `public static readonly IReadOnlyDictionary<string, IReadOnlyList<ModelInfo>> LanguageModels` with `StringComparer.OrdinalIgnoreCase`, key `"he"` only

### SettingsViewModel.cs changes

- Added `.Where(m => m.LanguageCode is null)` before `.Select(...)` in `AvailableModels` construction — enforces D-03: global Models tab lists only standard global models

### models/manifest.json changes

- Added `"ggml-medium-he"` entry with real SHA-256 `3d7881b4c606604e1b1f8695de0f41e48e7542ce4e7f6ef433a7040a985e0b96`, size 1,533,763,059, pinned commit URL matching ModelCatalog.cs

### ModelCatalogLanguageTests.cs (new file)

Six [Fact] tests: `LanguageModels_he_contains_ggml_medium_he`, `Arabic_not_in_LanguageModels`, `Find_ggml_medium_he_returns_non_null`, `ggml_medium_he_has_he_language_code`, `Global_models_have_null_language_code`, `LanguageModels_dictionary_is_case_insensitive`

## Autonomous Checkpoint Execution

Task 1 was a `checkpoint:human-verify` executed autonomously in YOLO mode:

```
curl -L -sS -o /tmp/ggml-hebrew.bin https://huggingface.co/imvladikon/whisper-medium-he/resolve/main/ggml-hebrew.bin
shasum -a 256 /tmp/ggml-hebrew.bin
→ 3d7881b4c606604e1b1f8695de0f41e48e7542ce4e7f6ef433a7040a985e0b96  /tmp/ggml-hebrew.bin
stat: 1533763059 bytes
x-repo-commit header: 241e89a758b512be3e5bae44dc93c2dc34873b81
```

SHA-256 cross-checked against `x-linked-etag` in HF response headers — values match, confirming content-addressed integrity.

## Deviations from Plan

None — plan executed exactly as written. The autonomous checkpoint execution was per YOLO mode instructions.

## Known Stubs

None. The Hebrew manifest entry contains a real computed SHA-256, not a placeholder.

## Threat Flags

None beyond what the plan's threat model already covers. T-08-01 and T-08-02 mitigations are implemented: pinned commit URL in HfHe const + manifest sha256 computed from actual downloaded bytes.

## TDD Gate Compliance

- RED gate: tests created first, failed with CS0117/CS1061 compile errors (LanguageCode and LanguageModels not yet defined)
- GREEN gate: implementation added, all 10 tests pass (commit 8ef8112)
- No REFACTOR commit needed — code matches patterns verbatim

## Self-Check: PASSED

- `ModelCatalogLanguageTests.cs` exists at tests/QuickSType.Core.Tests/ModelCatalogLanguageTests.cs ✓
- `ModelCatalog.cs` contains `LanguageCode` and `ggml-medium-he` ✓
- `SettingsViewModel.cs` contains `.Where(m => m.LanguageCode is null)` ✓
- `manifest.json` contains `ggml-medium-he` with 64-char hex SHA-256 (no TODO_COMPUTE) ✓
- Commit 8ef8112 exists in git log ✓
- 229 tests pass, 0 failures ✓
