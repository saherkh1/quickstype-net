---
phase: 02-config-model-picker
plan: 03
subsystem: persistence
tags: [csharp, dotnet, jsonl, persistence, aot, source-gen]

# Dependency graph
requires:
  - phase: 02-config-model-picker
    plan: 01
    provides: RED test files for HistoryService (wave 0)
provides:
  - HistoryEntry record (7-field D-07 JSONL schema)
  - IHistoryService port interface
  - HistoryService concrete append-only JSONL writer
  - ConfigJsonContext extension with HistoryEntry source-gen registration
affects: [plan 05 (wire HistoryService into DictationEngine), v1.1 history viewer]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "JSONL append-only persistence with source-gen AOT-safe serialization"
    - "Platform-aware path resolution sibling to ModelCatalog.ModelsDirectory"
    - "Newline stripping from WriteIndented=true JSON to produce compact JSONL lines"

key-files:
  created:
    - src/QuickSType.Core/History/HistoryEntry.cs
    - src/QuickSType.Core/History/IHistoryService.cs
    - src/QuickSType.Core/History/HistoryService.cs
  modified:
    - src/QuickSType.Core/Config/JsonContext.cs

key-decisions:
  - "D-07 implemented: 7-field JSONL schema at sibling of models directory, write-only in v1"

patterns-established:
  - "HistoryService: sealed class, optional ILogger + overridePath constructor, platform-aware DefaultPath, try/catch I/O with LogWarning"

requirements-completed: [CONFIG-05]

# Metrics
duration: 5min
completed: 2026-05-09
---

# Phase 02: Config & Model Picker — Plan 03 Summary

**JSONL history schema (D-07): HistoryEntry record with 7 fields, source-gen registration in ConfigJsonContext, and append-only HistoryService with platform-aware path resolution — all Wave-0 HistoryServiceTests now compile as GREEN**

## Performance

- **Duration:** 5 min
- **Started:** 2026-05-09T06:55:00Z
- **Completed:** 2026-05-09T07:00:00Z
- **Tasks:** 2 of 2
- **Files created:** 3
- **Files modified:** 1

## Accomplishments

- **HistoryEntry record** with 7 fields locked to D-07 contract: `v` (int=1), `ts` (string), `text` (string), `model` (string), `lang` (string), `duration_ms` (int), `device` (string?). Each field has explicit `[JsonPropertyName]` attribute matching D-07 exactly — Pitfall 6 hardened.
- **ConfigJsonContext source-gen registration** — `[JsonSerializable(typeof(QuickSType.Core.History.HistoryEntry))]` added, using the fully-qualified naming convention from existing `ModelManifest` entry.
- **IHistoryService port** — single method `AppendAsync(HistoryEntry, CancellationToken)` following `IPasteService.CancellationToken` pattern.
- **HistoryService concrete impl** — `AppendAsync` serializes via `ConfigJsonContext.Default.HistoryEntry`, strips WriteIndented newlines for compact JSONL, appends with `File.AppendAllTextAsync`, creates directory on first write, and wraps I/O in try/catch with `_log.LogWarning` (never throws to caller).
- **Platform-aware DefaultPath** — Mac: `~/Library/Application Support/QuickSType/history.jsonl`, Win: `%LOCALAPPDATA%\QuickSType\history.jsonl`, Linux: `$XDG_DATA_HOME/quickstype/history.jsonl`. Sibling (not child) of `ModelCatalog.ModelsDirectory()`.

## Task Commits

Each task was committed atomically:

1. **Task 1: Create HistoryEntry record + register with ConfigJsonContext** - `2b0a74d` (feat)
2. **Task 2: Create IHistoryService port + HistoryService impl with platform-aware DefaultPath** - `8523243` (feat)

## Files Created/Modified

- `src/QuickSType.Core/History/HistoryEntry.cs` — D-07 JSONL history line schema: sealed record with 7 explicitly-attributed fields
- `src/QuickSType.Core/History/IHistoryService.cs` — port interface: `Task AppendAsync(HistoryEntry, CancellationToken)`
- `src/QuickSType.Core/History/HistoryService.cs` — sealed class: append-only JSONL writer with platform-aware path, try/catch I/O, source-gen serialization with newline stripping
- `src/QuickSType.Core/Config/JsonContext.cs` — added `[JsonSerializable(typeof(QuickSType.Core.History.HistoryEntry))]`

## D-07 JSONL Schema (Byte-Exact Contract)

Each line in `history.jsonl` is one JSON object with these 7 keys:

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `v` | `int` | `1` | Schema version |
| `ts` | `string` | `""` | ISO-8601 UTC timestamp |
| `text` | `string` | `""` | Transcribed text |
| `model` | `string` | `""` | ModelInfo.Id (e.g., `"ggml-small"`) |
| `lang` | `string` | `""` | Active language code (e.g., `"en"`) |
| `duration_ms` | `int` | `0` | Audio duration in milliseconds |
| `device` | `string?` | `null` | SelectedAudioDevice or null |

Example line:
```json
{"v":1,"ts":"2026-05-09T14:32:00Z","text":"hello world","model":"ggml-small","lang":"en","duration_ms":3200,"device":null}
```

## Decisions Made

- **D-07 implemented** — JSONL history schema with 7-field contract at models-directory sibling location. History is write-only in Phase 2; v1.1 will read the same format without migration.
- **Newline stripping strategy** — `ConfigJsonContext` uses `WriteIndented=true` globally (`ConfigJsonContext.cs:5`), so `JsonSerializer.Serialize` produces multi-line JSON. `HistoryService.AppendAsync` strips `\r` and `\n` from the output before appending `\n` to create compact single-line JSONL entries. This avoids needing a second non-indented source-gen context.

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

- **SystemSpecsTests.cs still RED (plan 04 dependency)** — The test project cannot compile because `SystemSpecsTests.cs` references `SystemSpecsService` and `HardwareTier` types that are planned for plan 04. All 7 compilation errors are exclusively in `SystemSpecsTests.cs` (lines 8, 11-16). HistoryServiceTests compile and would pass, but `dotnet test` cannot execute because `dotnet build` of the full test project fails. This is the documented expected state from Wave 0.
  - Errors DOWN from 42 (pre-plan-03) to 7 (all SystemSpecsTests.cs, plan 04)

## Wiring Note

HistoryService is created but NOT yet wired into `AppHost` or `DictationEngine`. That integration happens in plan 05 when `HistoryService.AppendAsync` is called from `DictationEngine.ProcessAsync` after a successful paste, and `HistoryService` is instantiated in `AppHost.Create()`.

## Next Phase Readiness

- All 3 `HistoryServiceTests` test files compile against current codebase (GREEN once SystemSpecsTests.cs is resolved in plan 04)
- Plan 04 next: `SystemSpecsService`, `HardwareTier`, `SystemSpecs`, `ISystemSpecsService`
- After plan 04: plan 05 wires HistoryService into AppHost and DictationEngine

## Self-Check: PASSED

- FOUND: `src/QuickSType.Core/History/HistoryEntry.cs`
- FOUND: `src/QuickSType.Core/History/IHistoryService.cs`
- FOUND: `src/QuickSType.Core/History/HistoryService.cs`
- FOUND: `src/QuickSType.Core/Config/JsonContext.cs` (4 JsonSerializable attributes)
- FOUND: `.planning/phases/02-config-model-picker/02-03-SUMMARY.md`
- FOUND: Commit `2b0a74d` (Task 1 — HistoryEntry + JsonContext)
- FOUND: Commit `8523243` (Task 2 — IHistoryService + HistoryService)
- All grep acceptance checks pass (HistoryEntry record, IHistoryService interface, HistoryService class, source-gen ref, JsonSerializable)
---

*Phase: 02-config-model-picker*
*Completed: 2026-05-09*
