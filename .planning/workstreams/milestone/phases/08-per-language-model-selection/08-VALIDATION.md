---
phase: 8
slug: per-language-model-selection
status: certified
nyquist_compliant: true
wave_0_complete: true
created: 2026-05-24
audited: 2026-05-24
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 + Shouldly 4.3.0 |
| **Config file** | none (convention-based discovery) |
| **Quick run command** | `dotnet test tests/QuickSType.Core.Tests --filter "LanguageModels|MigrateV3ToV4|WithLanguageModel|ResolveModelId|LanguageModels_he"` |
| **Full suite command** | `dotnet test tests/QuickSType.Core.Tests` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run quick run command above
- **After every plan wave:** Run `dotnet test tests/QuickSType.Core.Tests`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 08-01-01 | 01 | 1 | AppConfig.LanguageModels field | T-08-03 | `ResolveModelId` validates model ID via `Find()` before use | unit | `dotnet test --filter "LanguageModels|MigrateV3ToV4|WithLanguageModel"` | ✅ ConfigRoundTripV4Tests.cs | ✅ green |
| 08-01-02 | 01 | 1 | v3→v4 migration + round-trip | — | N/A | unit | `dotnet test --filter "MigrateV3ToV4"` | ✅ ConfigMigrationV4Tests.cs | ✅ green |
| 08-02-01 | 02 | 1 | SHA-256 + manifest.json | T-08-02 | `ModelDownloader` verifies SHA-256 from manifest | integration | `dotnet test --filter "Every_catalog_model"` | ✅ ModelManifestTests.cs | ✅ green |
| 08-02-02 | 02 | 1 | `ModelCatalog.LanguageModels` dict | — | N/A | unit | `dotnet test --filter "LanguageModels_he"` | ✅ ModelCatalogLanguageTests.cs | ✅ green |
| 08-03-01 | 03 | 2 | `ResolveModelId` all branches + D-11 cancel | T-08-04 | Path traversal: `Find()` guard rejects unknown model IDs | unit | `dotnet test --filter "ResolveModelId"` | ✅ DictationEngineModelResolutionTests.cs | ✅ green |
| 08-03-02 | 03 | 2 | HUD `IsLoadingModel` state | — | N/A | build | `dotnet build src/QuickSType.UI -c Debug` | ✅ HudViewModel.cs | ✅ green |
| 08-04-01 | 04 | 2 | LanguageModelFlyoutViewModel | — | N/A | build | `dotnet build src/QuickSType.UI -c Debug` | ✅ LanguageModelFlyoutViewModel.cs | ✅ green |
| 08-04-02 | 04 | 2 | Gear button + DataTemplate | — | N/A | build | `dotnet build src/QuickSType.UI -c Debug` | ✅ MainWindow.axaml | ✅ green |
| 08-04-03 | 04 | 2 | UX end-to-end (assign + download flow) | D-04, D-10, D-11 | D-11: releasing hotkey during model load returns to Idle | manual | Human verification checkpoint | N/A | ⬜ manual |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `tests/QuickSType.Core.Tests/ConfigRoundTripV4Tests.cs` — `language_models` round-trip, empty dict default, v3 JSON without key → empty dict (not null), snake_case JSON key
- [ ] `tests/QuickSType.Core.Tests/ConfigMigrationV4Tests.cs` — `MigrateV3ToV4` bumps SchemaVersion to 4, injects empty dict, preserves all v3 fields; fall-through chain from v2→v3→v4
- [ ] `tests/QuickSType.Core.Tests/ModelCatalogLanguageTests.cs` — `LanguageModels["he"]` contains `ggml-medium-he`; Arabic not in map; `Find("ggml-medium-he")` returns non-null
- [ ] `tests/QuickSType.Core.Tests/DictationEngineModelResolutionTests.cs` — `ResolveModelId` for assigned, auto-language (D-12), and fallback cases; D-11 behavioral test: OnHotkeyReleased during LoadingModel cancels and returns to Idle

*Wave 0 test files must be created in Plan 08-01 Task 2 (Config tests) and 08-03 Task 1 (Engine tests). Existing `ModelManifestTests.Every_catalog_model_has_a_manifest_entry` serves as the integration gate for manifest.json completeness.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Hebrew gear button opens flyout with `ggml-medium-he` listed | D-01, D-05 | Avalonia UI requires visual inspection | Open Settings → Languages tab → click Hebrew gear icon → flyout shows `ggml-medium-he` entry |
| Arabic gear button shows only "Use global model" | D-06 | Requires Arabic to be added as an active language | Add Arabic in Languages tab → click gear → only "Use global model" option shown |
| Download button triggers progress bar in flyout | D-04 | Requires actual network download | Click Download for `ggml-medium-he` → progress bar appears and fills |
| HUD shows amber "Loading model…" on first Hebrew press | D-10 | Real audio + model swap path | Press hotkey with Hebrew active and model not loaded → HUD shows loading indicator before recording starts |
| Early hotkey release during load → Idle, no transcription | D-11 | Requires real model + timing | Press then immediately release hotkey during loading → engine returns to Idle, no text output |
| Global Models tab does NOT show `ggml-medium-he` | D-03 | Visual regression check | Open Settings → Models tab → Hebrew Medium not listed in global model dropdown |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify (08-04 Tasks 1+2 are build-only — only 2 consecutive; within Nyquist limit)
- [x] Wave 0 covers all MISSING references (4 new test files, created within their owning tasks before automated verify runs)
- [x] No watch-mode flags
- [x] Feedback latency < 10s (quick filter run)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** 2026-05-24

---

## Validation Audit 2026-05-24

| Metric | Count |
|--------|-------|
| Tasks audited | 9 |
| Gaps found | 0 |
| Resolved | 0 |
| Escalated to manual-only | 0 |

**Results:** All 4 Wave 0 test files exist. Full suite: 249 tests passed, 0 failed. UI build: 0 errors (2 pre-existing NU1903 warnings only). All plan acceptance-criteria greps pass. Anti-pattern grep (Task.Run ct-overload) returns 0. No gaps — phase is Nyquist-compliant as declared.
