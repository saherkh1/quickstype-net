# Phase 8: Per-Language Model Selection - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-24
**Phase:** 8-per-language-model-selection
**Areas discussed:** Settings UI placement, Arabic/Hebrew model catalog, Model swap timing, Auto-language interaction

---

## Settings UI Placement

| Option | Description | Selected |
|--------|-------------|----------|
| Inline per language row | Each language row gets a ComboBox dropdown inline | |
| Separate 'Per-Language Models' section | Dedicated section/tab with a grid of pairs | |
| Modal/flyout per language | Clicking a language opens a flyout | ✓ |

**User's choice:** Modal/flyout per language

---

| Option | Description | Selected |
|--------|-------------|----------|
| Small Avalonia Popup anchored to the row | Lightweight Popup within the window | |
| Full Avalonia Dialog window | Separate Dialog like first-run model picker | |
| You decide | Leave Popup vs Dialog to Claude | ✓ |

**User's choice:** You decide

---

| Option | Description | Selected |
|--------|-------------|----------|
| Settings icon / gear button per row | Small gear/cog button per language row | ✓ |
| Click anywhere on the row | Row click opens flyout | |
| Model name badge/chip on the row | Clickable chip showing current model | |

**User's choice:** Settings icon / gear button per row

---

| Option | Description | Selected |
|--------|-------------|----------|
| Show model name as muted text next to gear | Inline display of assigned model | |
| Gear icon only, model shown inside flyout | Cleaner row, detail only inside flyout | ✓ |

**User's choice:** No inline model name — gear icon only, model shown inside flyout

---

## Arabic/Hebrew Model Catalog

| Option | Description | Selected |
|--------|-------------|----------|
| Ship Hebrew fine-tune now; defer Arabic | Add imvladikon/whisper-medium-he; Arabic deferred | |
| Use a community-converted Arabic ggml model | Self-convert or find an Arabic ggml | |
| Arabic = large-v3-turbo-q5 default only | No fine-tune needed for Arabic | |

**User's choice (free-text):** Default for all languages = global recommended model. User can change per-language. Each language should have a curated list of fine-tuned models. If no ggml fine-tune exists for a language, show only global models (no dynamic HF API — curated catalog only).

---

| Option | Description | Selected |
|--------|-------------|----------|
| Curated catalog only | Static per-language list shipped with app | ✓ |
| Static curated + HF search link | Curated first, then link to HF browse | |
| Dynamic HF API list | App queries HF API at runtime | |

**User's choice:** Curated catalog only (for v1)

---

| Option | Description | Selected |
|--------|-------------|----------|
| Add as named catalog entry alongside global models | Hebrew in ModelCatalog.All, tagged to "he" | ✓ |
| Add only to per-language model list | Hebrew not in global catalog | |
| You decide | Claude decides cleanest approach | |

**User's choice:** Add as a named catalog entry alongside global models

---

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — show fine-tuned models globally | Hebrew fine-tune in global picker too | |
| No — fine-tuned models only in per-language flyout | Global picker stays clean | ✓ |

**User's choice:** No — show fine-tuned models only in the per-language flyout

---

## Model Swap Timing

| Option | Description | Selected |
|--------|-------------|----------|
| Lazy — load at next dictation start | EnsureLoaded() just before recording begins | ✓ |
| Eager — pre-warm in background on language switch | Background Task.Run on language change | |
| You decide | Claude picks based on hardware speed | |

**User's choice:** Lazy — load at next dictation start

---

| Option | Description | Selected |
|--------|-------------|----------|
| HUD shows 'Loading model...' state | HUD appears with loading indicator; recording starts after load | ✓ |
| Delay dictation start — nothing visual | Silent buffer until model loads | |
| You decide | Claude picks most consistent approach | |

**User's choice:** HUD shows 'Loading model...' state, hotkey held until ready

---

| Option | Description | Selected |
|--------|-------------|----------|
| Cancel the load and go to Idle | Release = cancel load, reset to Idle | ✓ |
| Finish loading, then transcribe captured audio | Continue load even after release | |
| You decide | Claude decides based on state machine | |

**User's choice:** Cancel the load and go to Idle

---

## Auto-Language Interaction

| Option | Description | Selected |
|--------|-------------|----------|
| No — auto-language uses global model only | Per-language map only for manual selection | ✓ |
| Yes — detected language drives model selection | Detected language triggers model swap mid-session | |

**User's choice:** No — auto-language uses the global model only

---

| Option | Description | Selected |
|--------|-------------|----------|
| Show download button inline in the flyout | Reuse ModelDownloader + progress inline | ✓ |
| Link to Settings model picker to download first | Toast redirect to Models tab | |

**User's choice:** Show download button inline in the flyout

---

## Claude's Discretion

- Popup vs Dialog implementation for the per-language model flyout
- Thread management for wrapping `Transcriber.EnsureLoaded()` in `Task.Run`
- Whether to add a new DictationEngine state for "loading model" or reuse a transitional state

## Deferred Ideas

- Dynamic HF API model browser per language — adds runtime network complexity
- Arabic fine-tuned ggml model — no pre-built community model currently available
- Auto-language → model swap based on detected language — mid-session swap complexity
- Expanding per-language catalog beyond Arabic/Hebrew to other languages
