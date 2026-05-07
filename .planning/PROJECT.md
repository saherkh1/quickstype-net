# QuickSType

## What This Is

QuickSType is a cross-platform (macOS + Windows) push-to-talk voice dictation desktop app. Hold a hotkey, speak, release — the transcribed text appears at your cursor. 100% local transcription via whisper.cpp through Whisper.net (Metal on Mac, CUDA on Windows). Distributed as a single AOT-compiled binary per platform. This milestone takes the existing .NET codebase from working prototype to a polished, open-source v1 release.

## Core Value

Streaming, live-as-you-speak dictation that feels effortless and stays 100% local — words appear in the focused text field within ~500ms of being spoken, no cloud, no account, no friction.

## Requirements

### Validated

<!-- Existing capabilities inferred from the codebase map at .planning/codebase/ (mapped 2026-05-07). -->

- ✓ Hold-to-talk global hotkey (default Right Option / Right Alt; rebindable) — existing
- ✓ Local Whisper transcription via Whisper.net + whisper.cpp (Metal on Mac, CUDA on Windows) — existing
- ✓ Tray / menu-bar icon with three states (idle / recording / processing) — existing
- ✓ Quick language switch from tray menu (16 supported languages) — existing
- ✓ First-run wizard: mic permission, model download, mic test — existing
- ✓ Settings window: model picker, hotkey rebind, audio device, languages, start-on-login — existing
- ✓ Ports-and-adapters architecture (Core / Platform.Mac / Platform.Windows / UI) — existing
- ✓ Single AOT-compiled binary per platform (~40–60 MB) — existing
- ✓ macOS 13+ and Windows 10/11 x64 supported — existing
- ✓ Cross-platform paste via OS-native injection (CGEvent on Mac, SendInput on Windows) — existing

### Active

<!-- v1 milestone scope: take QuickSType from working prototype to public open-source release. -->

- [ ] **Recording HUD** — animated overlay anchored to the tray/menu-bar icon with live waveform, red REC dot, and elapsed timer. Translucent dark, click-through, fades in/out. Drops *down* from the macOS menu bar; rises *up* from the Windows taskbar tray.
- [ ] **Streaming inline transcription** — words stream into the focused text field as the user speaks, target <500ms perceived latency on Apple Silicon and CUDA-equipped Windows. Auto-degrades to commit-on-pause mode on hardware that can't keep up. This is the protected core feature — last to cut if scope slips.
- [ ] **Keyboard-layout language auto-detection** — detect the active keyboard layout of the foreground window and force Whisper to output that language. Manual override remains available from the tray menu. Falls back to current tray-selected language if the layout has no Whisper match.
- [ ] **Spec-based first-run model picker** — automatic default-model selection based on hardware: Apple Silicon → `small`; Intel Mac or Windows with <8 GB RAM → `tiny`; <16 GB → `base`; ≥16 GB or CUDA-equipped → `small`. Disk ≥1 GB required. Clear blocker dialog if no model fits.
- [ ] **Visual polish across HUD + settings + first-run wizard** — translucent dark theme with system-themed accents (NSVisualEffectView on Mac, AcrylicBrush on Windows), Inter font, OS accent-color tracking, light-mode support, subtle motion.
- [ ] **Signed and notarized installers** — signed/notarized DMG for macOS, signed installer for Windows, attached as artifacts to GitHub Releases.
- [ ] **CI release pipeline** — automated builds on tag for both platforms, publishing to GitHub Releases.
- [ ] **In-app auto-update** — Sparkle on macOS, Velopack on Windows. Background update checks with user-visible "update available" notification.
- [ ] **Opt-in crash telemetry** — off by default; user must enable in settings. Crash-only, no usage analytics.
- [ ] **Homebrew tap + Winget manifest** — distribution via `brew install --cask quickstype` (or equivalent) and Microsoft `winget install`.
- [ ] **First public GitHub publish** — history scrub, secret scan, CONTRIBUTING.md, CODE_OF_CONDUCT.md, issue/PR templates, polished README, MIT license file, repo flipped public.

### Out of Scope

<!-- Explicit boundaries from this questioning session. -->

- **Cloud transcription / user accounts** — Hard product principle. The app stays 100% local; no login, no API option, no "send audio to a server" path. Network is used only for: signed model downloads from Hugging Face on first run, auto-update version checks, and opt-in crash reports.
- **Telemetry beyond opt-in crash reports** — Privacy posture. No usage analytics, no event tracking, no "phone home" behavior. Crash reports require the user to flip a setting.
- **Linux as a first-class release target** — Deferred (not banned). Linux currently runs via Noop services in `Composition/AppHost.cs`; we keep that fallback but don't ship Linux installers, CI, or tested support in this milestone.
- **Custom vocabulary / model fine-tuning** — Deferred. Stock Whisper models only in v1; no personal lexicon UI, no fine-tune workflow.

## Context

**Codebase state (2026-05-07).** Mature .NET 10 codebase with five projects: `QuickSType.Core` (cross-platform domain), `QuickSType.Platform.Mac` and `QuickSType.Platform.Windows` (P/Invoke adapters), `QuickSType.UI` (Avalonia 11.2 entry point + composition root + tray + settings), and `QuickSType.Core.Tests` (xunit). Architecture is ports-and-adapters / Hexagonal — `Core` defines `IPasteService`, `IPermissionService`, `IAutoLaunchService`, `INotificationService`; the platform projects implement them.

**Predecessor.** Successor to a Python POC at `mac-quickStype` (frozen at `v0.1.0-poc`). The .NET rewrite is the active codebase and is what ships.

**Existing transcription pipeline.** SharpHook (global hotkey) → PortAudioSharp2 (audio capture, runs on the native audio thread with `unsafe` pointer copy) → `ConcurrentQueue<float[]>` handoff → `DictationEngine` orchestration → Whisper.net (whisper.cpp through Metal/CUDA) → platform paste service. Current behavior is decode-on-release: audio is buffered, then transcribed, then pasted as one block. Streaming inline transcription is the major engineering shift in this milestone.

**Repo state.** Local-only; never pushed to GitHub. First public push is part of this milestone — clean history, secret scan, license/CONTRIBUTING/CoC, then push.

**Known concerns from the codebase map** (`.planning/codebase/CONCERNS.md`):
- Several services swallow exceptions silently (`PortAudioCapture.Dispose`, `HotkeyService.Dispose`, `Mac/WindowsNotifications`, etc.) — some user-visible failures could be invisible. Worth surfacing during the polish phase.
- AltGr collision (Windows) and Accessibility-permission relaunch (Mac) are documented limitations in `docs/ARCHITECTURE.md`.

**Existing supported languages.** 16, defined in `src/QuickSType.Core/Languages.cs`. Auto-detect logic already exists for Whisper but is not currently driven by keyboard layout.

## Constraints

- **Tech stack** — C# / .NET 10 / Avalonia 11.2 / whisper.cpp via Whisper.net. No UI framework rewrite. — Existing investment; team velocity.
- **AOT compatibility** — Every project sets `<IsAotCompatible>true</IsAotCompatible>`. New libraries must not break AOT (no reflection-based serialization, etc.). — Required for single-binary distribution and fast startup.
- **Performance — streaming latency** — <500ms perceived word-to-screen latency on Apple Silicon and CUDA-equipped Windows. Older Intel Macs and low-RAM Windows boxes auto-fall-back to commit-on-pause. — User-stated quality bar.
- **Privacy / network surface** — 100% local transcription. The only outbound network calls are: (a) signed Whisper model downloads from Hugging Face, (b) auto-update version checks, (c) opt-in crash reports. — Hard product principle.
- **Binary size** — Target ~40–60 MB per platform per the README. — Distribution friction; download/install UX.
- **Platforms** — macOS 13+ (Apple Silicon + Intel) and Windows 10/11 x64. Linux remains best-effort via Noop services, not a release target. — Stated in README; matches existing build scripts.
- **License** — MIT (per current README). Will ship as MIT in the public repo. — Already documented.

## Key Decisions

<!-- Decisions made during initialization questioning (2026-05-07). -->

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Streaming inline transcription is the protected core feature | Identified as the single most differentiating capability — the experiential gap between this app and the existing decode-on-release prototype | — Pending |
| Recording HUD anchors to the tray icon (drops down on Mac, rises up on Win) | Makes the HUD read as "this app is listening" rather than a generic system overlay; asymmetry matches each OS's tray location | — Pending |
| Translucent-dark + system-themed visual style across HUD, settings, first-run | Native polish on both platforms with one set of XAML; light-mode + OS accent-color tracking come for free | — Pending |
| Keyboard layout drives Whisper output language; manual tray override remains | Matches the stated multilingual workflow (typing in different languages on the same machine); override handles layout-vs-spoken-language divergence | — Pending |
| Spec-based first-run model heuristic (no benchmark, no always-base default) | Predictable onboarding, fast first-run, no live perf measurement needed; falls back to clear blocker dialog if no model fits | — Pending |
| Full release pipeline (signed binaries + auto-update + Homebrew/Winget) | "Production-ready open source" must mean installable with one command, not "git clone && build" | — Pending |
| Repo will be published as MIT (existing README claim) | Already documented; no friction to flip | — Pending |
| Streaming latency target = <500ms perceived | User-stated quality bar; achievable with `tiny`/`base` on modern Apple Silicon/CUDA hardware via chunked decoding | — Pending |
| Auto-update mechanism: Sparkle (Mac) + Velopack (Windows) | Mature, AOT-friendly options that align with each OS's update conventions | — Pending |
| Crash telemetry off by default; user must enable | Privacy posture is the brand promise; opt-in is the only way to keep it | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-05-07 after initialization*
