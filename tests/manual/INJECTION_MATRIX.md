# Injection Matrix — HUD-06 Cross-App Verification

Manual test matrix for streaming text injection and HUD behavior.
Run after each Phase 5 build; mark status and date.

## Legend

| Status | Meaning |
|--------|---------|
| PASS | Verified working |
| FAIL | Broken — see notes |
| DEFERRED | Not tested — reason in notes |

## macOS Targets

| # | Target App | HUD Appears No-Focus | Append Streams | Retract/Backspace | Focus Retained | Clicks Pass Through | Status | Verified By | Date | Notes |
|---|-----------|---------------------|----------------|-------------------|----------------|---------------------|--------|-------------|------|-------|
| 1 | TextEdit (plain text mode) | | | | | | DEFERRED | | | Pending Phase 5 build |
| 2 | Notes.app | | | | | | DEFERRED | | | Pending Phase 5 build |
| 3 | Safari URL bar | | | | | | DEFERRED | | | Pending Phase 5 build |
| 4 | Gmail compose (browser) | | | | | | DEFERRED | | | Pending Phase 5 build |
| 5 | Slack message input | | | | | | DEFERRED | | | Pending Phase 5 build |
| 6 | VS Code editor area | | | | | | DEFERRED | | | Pending Phase 5 build |

## Windows Targets

| # | Target App | HUD Appears No-Focus | Append Streams | Retract/Backspace | Focus Retained | Clicks Pass Through | Status | Verified By | Date | Notes |
|---|-----------|---------------------|----------------|-------------------|----------------|---------------------|--------|-------------|------|-------|
| 7 | Notepad | | | | | | DEFERRED | | | Deferred to Windows CI/manual pass |
| 8 | Edge/Chrome URL bar | | | | | | DEFERRED | | | Deferred to Windows CI/manual pass |
| 9 | Gmail compose (browser) | | | | | | DEFERRED | | | Deferred to Windows CI/manual pass |
| 10 | Slack message input | | | | | | DEFERRED | | | Deferred to Windows CI/manual pass |
| 11 | VS Code editor area | | | | | | DEFERRED | | | Deferred to Windows CI/manual pass |

## Notes

- macOS targets require Accessibility permission granted to QuickSType
- Windows targets to be verified in a CI or manual Windows pass
- Retract/Backspace column: does a `RetractChars > 0` update correctly backspace and replace?
- Focus Retained: after injection, does focus stay in the target app (not QuickSType)?
- Clicks Pass Through: can the user click through the HUD overlay to the app behind it?
