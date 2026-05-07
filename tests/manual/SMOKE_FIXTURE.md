# Smoke Fixture Contract — Phase 1 / D-Q5

**Chosen candidate:** "testing" solo
**Trial date:** 2026-05-08
**Trial RID:** osx-arm64 (Apple Silicon Mac)
**Trial count:** 10
**Pass count:** 10/10 (requirement met)

## Substring contract (used by 01-06-PLAN.md SmokeHarness)

`transcript.Trim().ToLowerInvariant().Contains("testing")` MUST be true.

The canonical substring is **"testing"** (case-insensitive, tolerates leading/trailing whitespace and punctuation).

## Must-not-contain probe

The transcript must NOT contain any of:
- "thanks for watching"
- "subscribe"
- "please like and subscribe"

(These are common Whisper-hallucination markers per RESEARCH.md Pitfall 6.)

## Maximum length

`transcript.Length <= 50` characters.

## Trial log (10 trials, recorded locally on osx-arm64)

| # | Transcript | Pass? |
|---|------------|-------|
| 1 | Testing. | ✓ |
| 2 | Testing. | ✓ |
| 3 | Testing. | ✓ |
| 4 | Testing. | ✓ |
| 5 | Testing. | ✓ |
| 6 | Testing. | ✓ |
| 7 | Testing. | ✓ |
| 8 | Testing. | ✓ |
| 9 | Testing. | ✓ |
| 10 | Testing. | ✓ |

## Analysis

The "testing" fixture produced deterministic, perfect transcriptions across all 10 trials on macOS arm64 with `ggml-tiny` model. The output is consistently "Testing." (capitalized, with trailing period — output of Whisper-tiny's normalization).

The fixture file is small (43 KB), well-formed PCM float32 at 16 kHz mono, and the utterance is clear and unambiguous.

**Note on cross-RID stability:** This spike was run locally on osx-arm64 only. The CI matrix in Wave 2 (01-05-PLAN.md / HARDEN-05) will validate the same fixture produces compatible transcripts on osx-x64 and win-x64. If cross-RID variance emerges (e.g., Whisper-tiny produces "test" on CPU vs "testing" on GPU), the substring assertion in the smoke harness will fail, and the fixture must be re-selected in a follow-up spike.
