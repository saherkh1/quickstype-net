# Telemetry Matrix — DIST-01 Signed-Build Verification

Manual test matrix for optional crash telemetry. Run only with signed release artifacts and a Sentry project owned by the release operator.

> **NOTE (2026-05-16):** All entries are PENDING until signed macOS and Windows artifacts exist. No row should be marked PASS without checking the Sentry event payload and confirming the app remains telemetry-off by default.

## Legend

| Status | Meaning |
|--------|---------|
| PASS | Verified working by a named tester on a signed release build |
| FAIL | Broken — see notes |
| DEFERRED | Not tested — reason in notes and accepted release risk |
| PENDING | Not yet tested |

## Operator Prerequisites

Before marking any row `PASS`:

- Use a signed/notarized macOS artifact or signed Windows artifact from the target GitHub Release.
- Set `QUICKSTYPE_SENTRY_DSN` to a Sentry DSN owned by the release operator.
- Set `QUICKSTYPE_SENTRY_ENVIRONMENT=release-smoke`.
- Record OS version, CPU architecture, artifact name, SHA-256, GitHub Release URL, Sentry issue/event URL, tester, and date in Notes.

## Verification Methods

Telemetry stays off unless both conditions are true:

1. User config has `"enable_crash_telemetry": true`.
2. `QUICKSTYPE_SENTRY_DSN` is present.

For smoke-event rows, run the signed executable with:

```bash
QUICKSTYPE_SENTRY_DSN="https://public@example.com/1" \
QUICKSTYPE_SENTRY_ENVIRONMENT="release-smoke" \
/Applications/QuickSType.app/Contents/MacOS/QuickSType --telemetry-smoke-test
```

On Windows, run the installed `QuickSType.exe --telemetry-smoke-test` from PowerShell with equivalent environment variables.

## Matrix

| ID | Platform | Scenario | Expected result | Status | Tester/date | Notes |
|----|----------|----------|-----------------|--------|-------------|-------|
| TELEMETRY-01 | macOS arm64 | Default config with Sentry DSN present | App does not initialize telemetry and no event appears | PENDING | - | Requires signed/notarized macOS artifact |
| TELEMETRY-02 | macOS arm64 | Opt-in config plus `--telemetry-smoke-test` | One Sentry event appears with `safe_marker=telemetry-smoke`; transcript, email, model path, and audio device are redacted | PENDING | - | Requires signed/notarized macOS artifact |
| TELEMETRY-03 | Windows x64 | Default config with Sentry DSN present | App does not initialize telemetry and no event appears | PENDING | - | Requires signed Windows artifact |
| TELEMETRY-04 | Windows x64 | Opt-in config plus `--telemetry-smoke-test` | One Sentry event appears with `safe_marker=telemetry-smoke`; transcript, email, model path, and audio device are redacted | PENDING | - | Requires signed Windows artifact |

## Closure Rule

DIST-01 can close only when every row is `PASS` with tester/date/notes evidence. To ship without that evidence, use the explicit unverified-v1 release acceptance path; do not mark DIST-01 complete.
