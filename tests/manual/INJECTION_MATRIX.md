# Injection Matrix — HUD-06 Cross-App Verification

Manual test matrix for streaming text injection and HUD behavior.
Run after each Phase 5 build; mark status and date.

> **NOTE (2026-05-16):** All entries are DEFERRED pending a full Phase 5 binary build and
> manual macOS/Windows testing session. No row should be marked PASS without evidence from
> an actual testing session with a signed Phase 5 binary and Accessibility permission granted
> to QuickSType. This matrix will be completed during the Phase 5 manual acceptance test.

## Legend

| Status | Meaning |
|--------|---------|
| PASS | Verified working — tested by a named tester on a real device |
| FAIL | Broken — see notes |
| DEFERRED | Not tested — reason in notes |

## Verification Method

To mark any row PASS, all five columns must be checked:
1. **HUD Appears No-Focus** — HUD overlay is visible on hotkey press; focused app remains focused (Accessibility Inspector or manual observation)
2. **Append Streams** — spoken words appear in the focused text field within ~500ms; no clipboard modification
3. **Retract/Backspace** — a correction utterance (mid-word retract) causes the expected number of backspaces followed by the replacement text
4. **Focus Retained** — after injection, the cursor remains in the target app field (not stolen by QuickSType)
5. **Clicks Pass Through** — clicking on the HUD overlay area interacts with the underlying app; HUD is not interactable

## macOS Targets

| # | Target App | HUD Appears No-Focus | Append Streams | Retract/Backspace | Focus Retained | Clicks Pass Through | Verification Method | Status | Verified By | Date | Notes |
|---|-----------|---------------------|----------------|-------------------|----------------|---------------------|---------------------|--------|-------------|------|-------|
| 1 | TextEdit (plain text mode) | | | | | | Phase 5 binary: hold hotkey → speak → release | DEFERRED | | | Pending Phase 5 binary build and manual macOS testing pass |
| 2 | Notes.app | | | | | | Phase 5 binary: hold hotkey → speak → release | DEFERRED | | | Pending Phase 5 binary build and manual macOS testing pass |
| 3 | Safari URL bar | | | | | | Phase 5 binary: focus URL bar, hold hotkey, speak domain name | DEFERRED | | | Pending Phase 5 binary build and manual macOS testing pass |
| 4 | Gmail compose (browser) | | | | | | Phase 5 binary: open compose, hold hotkey, speak sentence | DEFERRED | | | Pending Phase 5 binary build and manual macOS testing pass |
| 5 | Slack message input | | | | | | Phase 5 binary: focus message box, hold hotkey, speak | DEFERRED | | | Pending Phase 5 binary build and manual macOS testing pass |
| 6 | VS Code editor area | | | | | | Phase 5 binary: open file, place cursor, hold hotkey, speak code | DEFERRED | | | Pending Phase 5 binary build and manual macOS testing pass |

## Windows Targets

| # | Target App | HUD Appears No-Focus | Append Streams | Retract/Backspace | Focus Retained | Clicks Pass Through | Verification Method | Status | Verified By | Date | Notes |
|---|-----------|---------------------|----------------|-------------------|----------------|---------------------|---------------------|--------|-------------|------|-------|
| 7 | Notepad | | | | | | Phase 5 Windows binary: hold hotkey → speak → release | DEFERRED | | | Pending Phase 5 binary build and manual Windows testing pass |
| 8 | Edge/Chrome URL bar | | | | | | Phase 5 Windows binary: focus URL bar, hold hotkey, speak domain | DEFERRED | | | Pending Phase 5 binary build and manual Windows testing pass |
| 9 | Gmail compose (browser) | | | | | | Phase 5 Windows binary: open compose, hold hotkey, speak sentence | DEFERRED | | | Pending Phase 5 binary build and manual Windows testing pass |
| 10 | Slack message input | | | | | | Phase 5 Windows binary: focus message box, hold hotkey, speak | DEFERRED | | | Pending Phase 5 binary build and manual Windows testing pass |
| 11 | VS Code editor area | | | | | | Phase 5 Windows binary: open file, place cursor, hold hotkey, speak code | DEFERRED | | | Pending Phase 5 binary build and manual Windows testing pass |

## Acceptance Criteria for Any Row to be Marked PASS

- [ ] Phase 5 binary built and signed (or ad-hoc signed for development)
- [ ] QuickSType has Accessibility permission (macOS: System Settings → Privacy → Accessibility)
- [ ] Model downloaded and loaded (ggml-base or larger)
- [ ] Hotkey held: HUD appears within 200ms, focused app retains focus
- [ ] Speech detected: words stream into the focused text field within ~500ms
- [ ] Retract test: mid-word correction causes correct number of backspaces + replacement
- [ ] Hotkey released: HUD hides within 200ms
- [ ] Click-through confirmed: clicking HUD area interacts with app behind it

## Notes

- macOS targets require Accessibility permission granted to QuickSType
- Windows targets to be verified in a CI or manual Windows pass
- Retract/Backspace column: does a `RetractChars > 0` update correctly backspace and replace?
- Focus Retained: after injection, does focus stay in the target app (not QuickSType)?
- Clicks Pass Through: can the user click through the HUD overlay to the app behind it?
- No row should be marked PASS based on code inspection alone — real-device testing required
