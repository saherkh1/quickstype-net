---
type: codebase-map
focus: concerns
last_mapped: 2026-05-07
---

# QuickSType.NET — Concerns Map

Severity legend: **Low** = cosmetic / unlikely to bite | **Medium** = real bug or likely user-visible | **High** = data loss, crash, security, or correctness in the hot path.

## TODOs / FIXMEs / HACKs

No findings in `src/` or `tests/` C# code.

The only `TODO` markers in the tree live inside the bundled whisper.cpp Metal shader (`src/QuickSType.UI/bin/Debug/net10.0/ggml-metal.metal`), which is a third-party build artifact (not committed source). Severity: **N/A**.

## Tech debt

- **Swallowed exceptions (broad `catch { /* swallow */ }`)** — many. Severity: **Medium** in aggregate.
  - `src/QuickSType.Core/Audio/PortAudioCapture.cs:166` — `Dispose` swallows everything.
  - `src/QuickSType.Core/Hotkey/HotkeyService.cs:111` — `Dispose` swallows everything.
  - `src/QuickSType.Platform.Mac/MacAutoLaunch.cs:26,71,86` — file delete + `launchctl load/unload` swallowed.
  - `src/QuickSType.Platform.Mac/MacNotifications.cs:33,60` — `osascript`/`afplay` failures invisible.
  - `src/QuickSType.Platform.Mac/MacPermissions.cs:38,59` — AX prompt + `open` failures hidden.
  - `src/QuickSType.Platform.Windows/WindowsNotifications.cs:35,40,46` — `Beep` and PowerShell errors hidden.
  - `src/QuickSType.Platform.Windows/WindowsPermissions.cs:23` — `Start` failures hidden.
  - `src/QuickSType.UI/Composition/AppHost.cs:121` — `ConfigChanged` handler exceptions hidden.
  - `src/QuickSType.UI/Tray/TrayService.cs:240` — icon load failures silent.
  - `src/QuickSType.UI/Views/MainWindow.axaml.cs:62` — `OpenInFinder` swallowed.
  - At least these are *logged* before swallowing in some places (`PortAudioCapture.Stop`, hotkey handlers, paste services), which is fine. The unconditional empty swallows above lose all signal — settings UI changes appear to succeed but silently fail.
- **Hand-rolled clipboard-restore dance** duplicated nearly verbatim in `MacPasteService.PasteAsync` (`MacPasteService.cs:23-52`) and `WindowsPasteService.PasteAsync` (`WindowsPasteService.cs:25-51`). The `PasteSettleMs = 120` constant is duplicated in both (`MacPasteService.cs:12`, `WindowsPasteService.cs:11`). Severity: **Low**. Could be lifted into a `PasteServiceBase` helper.
- **Magic numbers** scattered. Severity: **Low**.
  - `PortAudioCapture.cs:10` `FramesPerBuffer = 1024` (OK, named).
  - `Transcriber.cs:56` `Math.Max(2, Environment.ProcessorCount / 2)` thread count not configurable.
  - `Transcriber.cs:45,83` `sampleRate / 5` and `sampleRate / 2` minimums repeated.
  - `ModelDownloader.cs:64,66` two separate `81920` magic numbers (FileStream buffer + read buffer).
  - `MacPasteService.cs:46,91` `PasteSettleMs = 120` and `WaitForExit(2000)` hard-coded.
  - `MacAutoLaunch.cs:69,84` `WaitForExit(2000)` hard-coded.
- **P/Invoke shape inconsistency** — `MacPermissions` uses `[LibraryImport]` for AX functions but falls back to legacy `[DllImport]` at line 108 (`CFStringCreateWithBytes`) inside a nested helper. `MacPasteService` and `WindowsPasteService` are clean `[LibraryImport]`. Severity: **Low** — not a correctness issue but breaks the AOT-friendly story locally.
- **`MacPermissions.HasMicrophoneAccess` and `HasInputMonitoringAccess` are stubs** that return `true` unconditionally (`MacPermissions.cs:9,11`). Same on `WindowsPermissions.cs:7-9`. The interface promises a check that does not exist; UI logic that depends on it (none today) would silently pass. Severity: **Medium**. Either drop the methods or implement them via `AVCaptureDevice.authorizationStatus` (Mac) / `Microsoft.Windows.SDK` (Win).
- **`MacAutoLaunch` writes a `.plist` that runs `Environment.ProcessPath` with no quoting** (`MacAutoLaunch.cs:44`). If the install path ever contains XML-special characters, the plist becomes malformed. Severity: **Low** (process paths don't normally hit this, but no escaping is done).
- **`MacNotifications.PlaySoundAsync` discards the `Process` handle** (`MacNotifications.cs:57` — assigns to `p` then never disposes), so the started `afplay` process leaks file handles until exit. Severity: **Low**.
- **`WindowsAutoLaunch` quotes the executable path but adds `--tray` argument** (`WindowsAutoLaunch.cs:28`) — `MacAutoLaunch` does not pass `--tray` (`MacAutoLaunch.cs:43`). Cross-platform parity issue (also see "Cross-platform parity gaps"). Severity: **Medium**.
- **`Transcriber` exposes `LoadedModelPath` as a property but never reads it inside the class** (`Transcriber.cs:21`); only `EnsureLoaded` checks it. Dead public surface. Severity: **Low**.
- **`AppConfig.Languages` is `List<string>` (mutable) on a `record`** (`Config.cs:11`). Records imply value semantics, but the list is shared by reference; mutating it bypasses `WithEnabledLanguages`. Severity: **Low**.

## Bugs / suspicious code

- **`HotkeyService.CaptureNextKeyAsync` is racy** (`HotkeyService.cs:43-57`). Severity: **Medium**.
  - Two concurrent calls overwrite `_captureTcs`, orphaning the first awaiter forever (it can only be cancelled by its own `Task.Delay`, which only TrySetCanceled if `_captureNext` is still true).
  - `_captureNext` is a non-volatile `bool` mutated from the SharpHook thread (in `OnKeyPressed`) and read from a `Task.Delay` continuation thread — no memory barrier.
  - The timeout closure captures the latest TCS via the local `tcs` variable (good), but the `if (_captureNext)` check race-conditions with `OnKeyPressed`: if a key arrives just as the timeout fires, both can succeed/cancel.
- **`HotkeyService._isHeld` mutated from hook thread without sync** (`HotkeyService.cs:13,68-83`). Severity: **Low**. SharpHook's `TaskPoolGlobalHook` runs handlers on pool threads, so a press immediately followed by release on different threads could see stale `_isHeld`. In practice rare, but not provably correct.
- **`PortAudioCapture.OnAudio` allocates a fresh `float[]` on every callback** (`PortAudioCapture.cs:150`). This runs from the PortAudio realtime thread at ~16 kHz / 1024 frames = ~15 callbacks/sec; not catastrophic but contributes to GC pressure during long dictations. The unsafe pointer copy is correct. Severity: **Low** (also Performance).
- **`PortAudioCapture._initialized` is read without `lock` in `EnsureInitialized`'s fast path** — wait, it does take `InitLock` always (`PortAudioCapture.cs:36`). Correct. No issue.
- **`PortAudioCapture` never calls `PortAudio.Terminate`** anywhere. Severity: **Low** — process exit reclaims it, but resource cleanliness is missing.
- **`DictationEngine.OnHotkeyReleased` cancels the previous CTS but does not await it** (`DictationEngine.cs:98-100`). Severity: **Medium**. If the user presses-releases-presses-releases quickly:
  1. First `ProcessAsync` is mid-`TranscribeAsync` (not cancellation-aware between segments — Whisper.net's `ProcessAsync` does honour the token but only at segment boundaries).
  2. Second release fires → cancels first CTS, starts second `ProcessAsync` on a new CTS.
  3. Both pipelines race to call `_paste.PasteAsync(text, ct)` and `Transcribed?.Invoke(text)`. The state machine guards against simultaneous *recordings* but not simultaneous *processings*.
  Result: occasional double-paste or out-of-order paste under rapid hotkey use.
- **`DictationEngine.ProcessAsync` calls `_transcriber.EnsureLoaded(modelPath, useGpu)` synchronously on the calling thread** (`DictationEngine.cs:124`). This is the first big I/O on hotkey release — load can take 1–5 seconds for a Q5 turbo model and runs on whatever thread `Task.Run` placed it on. The call is currently invoked via `_ = ProcessAsync(...)` from `OnHotkeyReleased` (which itself is invoked from a SharpHook pool thread), so this is OK… but `EnsureLoaded` holds `_lock` and would block any other transcription. Severity: **Low**.
- **`Transcriber.PrewarmAsync` swallows errors silently for warm-up failures** (`Transcriber.cs:90-93`) — that's fine, but it logs a *warning* without indicating to the user that the chosen GPU backend may be broken. Severity: **Low**.
- **`ModelDownloader.DownloadAsync` doesn't atomically replace** when the temp file is empty/truncated mid-stream. Severity: **Medium**.
  - Resume logic at line 36-40 reads `partPath` length, but does NOT verify that the server returns a `206 Partial Content` — `EnsureSuccessStatusCode` accepts both 200 and 206. If the server returns full 200 (some CDNs do), the code will append fresh bytes to existing partial bytes → corrupt model file. The `ContentRange?.Length` check at line 58 only covers the `total` calculation, not the integrity of the appended stream.
  - There is **no checksum or signature verification** after download — the renamed file is trusted blindly (also see "Security").
  - The `if (File.Exists(finalPath))` short-circuit at line 42 happens *after* the resume detection at line 36, meaning a previous corrupt `.part` survives if `finalPath` exists. Cosmetic — but gives the user a stale residual file.
- **`ModelDownloader` `Http` is a `static readonly HttpClient`** with `Timeout = 60 minutes` (`ModelDownloader.cs:8-11`). `SocketsHttpHandler` defaults are otherwise fine, but the long timeout combined with `using var response` means a stalled download holds a socket for an hour before timing out. No `ResponseDrainTimeout` set. Severity: **Low**.
- **`MacPasteService.PasteAsync` does not preserve clipboard image/file data** — it only round-trips text via `ClipboardService.GetTextAsync`/`SetTextAsync` (`MacPasteService.cs:27,50`). If the user had an image or file copied, it gets clobbered with text and "restored" as text. Severity: **Medium**. Same on Windows (`WindowsPasteService.cs:30,48`).
- **Paste services use a fixed 120 ms `Task.Delay` before restoring the previous clipboard** (`MacPasteService.cs:46`, `WindowsPasteService.cs:44`). Severity: **Medium**. On a slow/laggy target app, the OS may not have consumed the Cmd+V/Ctrl+V before we overwrite the clipboard, resulting in the user pasting *the previous clipboard contents* instead of the transcribed text. There's no "verify paste happened" mechanism (e.g. on macOS no `kCGEventTapPostListened` wait).
- **`MacPasteService.SendCmdV` does not check if the AX permission is actually granted** before posting events (`MacPasteService.cs:55-77`). When AX is missing, `CGEventPost` silently fails — the user's clipboard is overwritten and "restored" but no paste fires, with no error path. The `AppKit` exception fallback to `osascript` (`MacPasteService.cs:42-44`) only catches a managed exception, which CG calls do not throw. Severity: **High** for first-run UX. Users without AX granted will see the indicator flip but get nothing pasted, with the original clipboard wiped.
- **`MacPasteService.SendCmdV` doesn't release `keyDown` and `keyUp` `IntPtr`s on exception** (`MacPasteService.cs:60-77`). The `try/finally` only releases the source. If `CGEventCreateKeyboardEvent` succeeds but `CGEventSetFlags` throws (it shouldn't, but..), the events leak. Severity: **Low**.
- **`MacAutoLaunch.SetEnabled` — the legacy `launchctl load` is deprecated** in modern macOS in favour of `launchctl bootstrap gui/<uid> <plist>`. `load` still works on Sequoia but emits a deprecation warning to stderr (which is swallowed). Severity: **Low**.
- **`async void` not used anywhere** — ✓ clean.
- **No blocking `.Result` / `.Wait()` calls in async code paths** — ✓ clean.
- **`SettingsViewModel.TestTranscription` calls `_host.Audio.Start()` while the dictation engine could be running** (`SettingsViewModel.cs:185`). If the user presses the global hotkey during a "Test transcription", both the engine and the test will race on the same `IAudioCapture` instance (the engine's `Start` will silently early-out, but the test's `Stop` will dump samples that include the engine's recording). Severity: **Medium**.
- **`SettingsViewModel.OnLanguageRowChanged` re-runs on every checkbox tick** (`SettingsViewModel.cs:99-106`), each one calling `_host.UpdateConfig` which writes the full config to disk via `ConfigStore.Save`. Bulk-toggling N languages = N disk writes. Severity: **Low** (file is small, but unnecessary).
- **`TrayService` subscribes to `_host.Engine.StateChanged` in the constructor but never unsubscribes** (`TrayService.cs:31`). `TrayService` itself has no `Dispose` and lives for the app lifetime, so this is not a real leak. Severity: **Low**.
- **`MainWindow.WireButtons` searches by name without null-check after assignment to `OpenLogsButton`** — actually it does null-check (`MainWindow.axaml.cs:30,31`). OK.
- **`App.OnFrameworkInitializationCompleted` fires `_ = Host.StartAsync()` and never observes the task** (`App.axaml.cs:30`). Since `StartAsync` calls `_ = Hotkey.RunAsync()` which itself returns a long-running task, an exception in the global hook would be silently lost. Severity: **Medium** — losing the hotkey is a fatal user-visible failure mode that today produces zero log output.

## Security

- **No checksum / signature verification on Whisper model downloads** — `ModelDownloader.DownloadAsync` (`ModelDownloader.cs:25-96`) downloads from `https://huggingface.co/...` over HTTPS (good) but trusts whatever bytes arrive. A compromised CDN, MITM that bypasses cert validation, or DNS poisoning lands an attacker-controlled `.bin` that whisper.net loads as a native model. Whisper model files are read by native code (ggml) and could plausibly be used to trigger memory-corruption bugs in ggml. Severity: **High** for a dictation app that publishes binaries — at minimum each entry in `ModelCatalog.All` (`ModelCatalog.cs:14-23`) should ship a SHA-256 that the downloader verifies after rename and deletes if it doesn't match.
- **HuggingFace URLs use the `resolve/main` branch** (`ModelCatalog.cs:12`) which is mutable. Pinning a commit SHA (`resolve/<sha>/...`) gives content-addressable safety. Severity: **Medium**.
- **Config file written without restrictive permissions** — `ConfigStore.Save` (`ConfigStore.cs:83-93`) does `File.WriteAllText` with default umask, which on macOS/Linux yields `644`. The file currently contains no secrets, but anything added later (API keys for cloud transcription, etc.) inherits world-readable mode. Severity: **Low** (today, no secrets stored).
- **No plaintext credentials found** anywhere in `src/` ✓.
- **AX trust prompt** — `MacPermissions.RequestAccessibilityIfNeeded` (`MacPermissions.cs:25-41`) sets `AXTrustedCheckOptionPrompt: true`. This is the correct behaviour, but the app *also* declares `HasMicrophoneAccess() => true` (`MacPermissions.cs:9`) without ever calling `AVCaptureDevice.requestAccess(forMediaType:)`. The TCC system will prompt automatically when PortAudio first opens the input device, but if the user denies, the app has no path to detect it (`HasMicrophoneAccess` lies). Severity: **Medium**.
- **Windows elevation requirements** — `WindowsAutoLaunch` writes `HKCU\...\Run` (`WindowsAutoLaunch.cs:8`), which is per-user and does not require elevation ✓. `WindowsPasteService.SendInput` does not work against elevated targets when the app itself is not elevated — known Windows UIPI limitation. The code does not attempt to detect or surface this. Severity: **Low** (documented Windows behaviour, but worth noting in UI).
- **`MacNotifications.Notify` shells `osascript -e $script` with user-controlled `title`/`message`** (`MacNotifications.cs:14-37`). The custom `Escape` (`MacNotifications.cs:66`) only handles `\` and `"`. AppleScript is sensitive to other characters (e.g. `}` in some locales, smart quotes, embedded `--`). For the *current* call sites — `Notify("QuickSType", text.Length > 80 ? text[..80] + "…" : text)` — the input is the transcribed user speech, so a malicious actor would have to speak an exact AppleScript-breaking sentence to break their own app. Severity: **Low**, but the right fix is to pass the values via stdin or `-s s -e ... read text from stdin` instead of building a script string.
- **`WindowsNotifications.Notify` builds a PowerShell script and passes it as `-Command` after `Replace("\"", "\\\"")`** (`WindowsNotifications.cs:28`). PowerShell quoting is notoriously hard; a transcribed sentence containing `'` or `;` could change the script. Same comment as Mac — input is the user's own speech, but this is a **command-injection-shaped** code path that would be problematic if the message source ever became external. Severity: **Medium**.

## Performance / resource concerns

- **Per-callback `float[]` allocation in audio hot path** — `PortAudioCapture.OnAudio` allocates a fresh array every ~64 ms (`PortAudioCapture.cs:150`). With dictations of seconds-to-minutes, this is fine but is also the cheapest GC win available (use `ArrayPool<float>` and a `ConcurrentQueue<(float[] buf, int len)>`). Severity: **Low**.
- **`DrainChunks` re-allocates the merged buffer** (`PortAudioCapture.cs:124-141`) — single `new float[total]` then per-chunk `Array.Copy`. Fine for short recordings; for very long recordings (>5 min) this is one large gen-2 allocation. Severity: **Low**.
- **`WavWriter.WritePcm16` allocates a full byte array sized for the entire recording** (`WavWriter.cs:16`). 60 s @ 16 kHz mono pcm16 = ~1.9 MB — fine. 30 min would be ~57 MB on the LOH. Severity: **Low**.
- **Whisper model load is synchronous and on the calling thread** — `Transcriber.EnsureLoaded` (`Transcriber.cs:23-34`) under a `lock`. First press after launch will block ~1-5 s before any audio processing; UI thread is *not* blocked because the call happens from `ProcessAsync` (`DictationEngine.cs:124`) which is on a pool thread. There's a `PrewarmAsync` (`Transcriber.cs:80-94`) that's never called from `AppHost.StartAsync` (`AppHost.cs:109-113`). Severity: **Medium** — first-press latency is the most visible perf issue.
- **JSON config deserialization** — `ConfigStore.Load` runs on whatever thread calls it (today: `AppHost.Create` on UI thread during `OnFrameworkInitializationCompleted`). File is small (~1 KB), uses source-generated `JsonContext` (good), so no measurable cost. Severity: **Low**.
- **`Languages.Common.FirstOrDefault` linear scan** (`Languages.cs:28`) called from many hot UI paths and `AppConfig.WithEnabledLanguages`. List is 16 entries — fine. Severity: **Low**.
- **`Languages.Common`, `ModelCatalog.All` are `IReadOnlyList<…>` initialized via `new T[]`** — fine, single allocations at startup.
- **No unbounded collections found** — `_chunks` (`PortAudioCapture.cs:15`) grows during recording but is drained on every `Stop`. `_allLangItems` (`TrayService.cs:25`) sized to `Languages.Common.Count`. `AvailableModels`, `AvailableLanguages` (`SettingsViewModel.cs:33-35`) bounded.
- **`MacNotifications.Notify` spawns `osascript` and waits up to 1.5 s synchronously** on the calling thread (`MacNotifications.cs:31`). Called from `DictationEngine.ProcessAsync` (`DictationEngine.cs:139`) — pool thread, so doesn't freeze UI, but does delay the `finally { SetState(Idle) }` by up to 1.5 s. Severity: **Low**.
- **`WindowsNotifications.Notify` spawns PowerShell** (`WindowsNotifications.cs:25-34`) — heavy. PowerShell startup can be 200–800 ms; for every transcription this is significant. Severity: **Medium**. Should use `Microsoft.Toolkit.Uwp.Notifications` or P/Invoke `Shell_NotifyIcon` directly.

## Cross-platform parity gaps

| Area | Mac | Windows | Severity |
|---|---|---|---|
| **Auto-launch executable args** | passes raw `exe` (`MacAutoLaunch.cs:43`) | passes `"<exe>" --tray` (`WindowsAutoLaunch.cs:28`) | **Medium** — auto-launched Mac instance opens the main window if `--tray` is the trigger, since `Program.Main` doesn't branch on `--tray` either way (today both platforms behave the same regardless, see below). |
| **`--tray` flag actually wired up?** | No — `Program.Main` (`Program.cs:8-19`) ignores all args. | No — same. | **Low** — both platforms pass the arg-or-not to a flag that is never read. The `--tray` wiring is a dead Windows-side-only convention. |
| **Permissions service** | Implements AX trust check via real CG call (`MacPermissions.cs:13-23`). Mic + Input Monitoring stub `true` (`MacPermissions.cs:9,11`). | All three checks stub `true` (`WindowsPermissions.cs:7-9`). | **Medium** — Windows has no real input-monitoring need (SendInput is unrestricted at non-elevated levels), so stubs are arguably correct. Mac mic + input-monitoring stubs are wrong. |
| **Notifications: sound** | Uses real `afplay` of system aiff (`MacNotifications.cs:42-64`). Distinct start vs stop sounds. | Uses `Beep(880, 80)` and `Beep(440, 80)` (`WindowsNotifications.cs:38-48`). Console-style beep, no respect for system audio routing or output device. | **Low** — both produce audio, but Windows users get a console-beep when Mac users get a system sound. |
| **Notifications: visual** | `osascript display notification` (`MacNotifications.cs:14-37`) — native macOS notification. | Spawns `powershell.exe` to use `System.Windows.Forms.NotifyIcon.ShowBalloonTip` (`WindowsNotifications.cs:9-36`) — heavy and uses the deprecated balloon API, not modern toast. | **Medium** — performance + visual quality both worse on Windows. |
| **AX / accessibility request** | Real prompt via `AXIsProcessTrustedWithOptions` (`MacPermissions.cs:25-41`). | No-op (`WindowsPermissions.cs:15`). | **Low** — Windows correctly has nothing to ask for. |
| **Open settings deep links** | All three Mac links go to specific Privacy panes (`MacPermissions.cs:43-50`). | `OpenInputMonitoringSettings` and `OpenAccessibilitySettings` both fall through to generic privacy/easeofaccess pages (`WindowsPermissions.cs:11-13`). | **Low**. |
| **Paste fallback** | Mac falls back to `osascript ... keystroke "v"` if CG fails (`MacPasteService.cs:42-44`). | Windows logs error and gives up (`WindowsPasteService.cs:39-42`). | **Low** — symmetry would be nice but Windows SendInput rarely fails. |
| **Tray service** | Single Avalonia `TrayIcon` works on both. | Same. | OK ✓ |
| **Auto-launch config storage** | `~/Library/LaunchAgents/com.saherk.quickstype.plist` (`MacAutoLaunch.cs:11-14`). | `HKCU\...\Run\QuickSType` (`WindowsAutoLaunch.cs:8-9`). | OK ✓ |

## Untested fragile areas

Existing tests cover: WAV writing, hotkey parsing, language validation, config migration, config round-trip, model catalog. **Not covered**:

- **`PortAudioCapture`** — no tests. Audio thread, native interop, device selection logic. Severity: **High** — most fragile component.
- **`HotkeyService`** — only `ParseKey`/`Format` tested (`HotkeyParsingTests.cs`). The `CaptureNextKeyAsync` race (see Bugs), the `_isHeld` state machine, and timeout cancellation all untested. Severity: **High**.
- **`DictationEngine`** — no tests. State machine (`Idle` → `Recording` → `Processing` → `Idle`), CTS cancellation semantics on rapid press-release-press, error propagation. Severity: **High**.
- **`Transcriber`** — no tests. Lock semantics, model swap on path change, language fallback. Severity: **Medium** — hard to unit test (requires Whisper.net + a model on disk), but the language/auto-lang branching logic in `TranscribeAsync` (`Transcriber.cs:58-63`) is pure and could be extracted + tested.
- **`ModelDownloader`** — no tests. Resume logic, partial-content vs full-content handling, atomic rename. Severity: **High** — Bugs section above identifies a real correctness issue here.
- **`MacPasteService` / `WindowsPasteService`** — no tests. Clipboard restore, paste timing, AX-missing failure mode. Severity: **High** for the user-visible "wrong text pasted" / "nothing pasted with clipboard wiped" classes of bug.
- **`ConfigStore` legacy migration path** is partly tested (`ConfigMigrationTests.cs`) but the file-on-disk path (`File.Replace` semantics on Windows vs `File.Move` on first save) is not directly tested.
- **`SettingsViewModel`** — no tests. The `OnLanguageRowChanged` debounce-less write storm, the `TestTranscription` race vs the engine, `OnConfigChanged` re-entrancy. Severity: **Medium**.
- **`TrayService`** — no tests (UI-thread Avalonia code, hard). Severity: **Low**.

## AOT / trimming readiness

All four `.csproj` files declare `<IsAotCompatible>true</IsAotCompatible>` (`QuickSType.Core.csproj:6`, `QuickSType.UI.csproj:10`, `QuickSType.Platform.Mac.csproj:6`, `QuickSType.Platform.Windows.csproj:6`). `<PublishAot>` is **not** set in any csproj nor in `Directory.Build.props`, so AOT is opt-in but the codebase is annotated as ready.

Findings:

- **`System.Text.Json` reflection** — config uses source-gen via `ConfigJsonContext` (`JsonContext.cs:11`) and consistently passes `ConfigJsonContext.Default.AppConfig`/`LegacyPythonConfig` into `JsonSerializer.Serialize`/`Deserialize` (`ConfigStore.cs:51,66,89`). ✓ AOT-safe.
- **`Type.GetType` / `Activator.CreateInstance`** — none in the codebase ✓.
- **Dynamic loading** — `MacPermissions.cs:75-77` uses `NativeLibrary.Load` + `NativeLibrary.GetExport` to fetch `kCFBooleanTrue`. This is AOT-safe (`NativeLibrary` is supported under AOT), but the exception path silently returns from `RequestAccessibilityIfNeeded` — fine. **Severity: Low**.
- **P/Invoke shape** — most are `[LibraryImport]` (AOT-friendly): `MacPasteService.cs:94-108`, `WindowsPasteService.cs:127`, `MacPermissions.cs:62-85`, `WindowsNotifications.cs:52`. **One legacy `[DllImport]`**: `MacPermissions.cs:108` (`CFStringCreateWithBytes`). Will produce a trimmer warning under `PublishAot` but still works (`DllImport` is supported, just not preferred). **Severity: Low**.
- **`AllowUnsafeBlocks=true`** in Core/Mac/Windows csprojs — fine for AOT, but limits how aggressively the linker can prove things.
- **Avalonia 11.2.3** — Avalonia advertises AOT support in 11.x. The project sets `AvaloniaUseCompiledBindingsByDefault=true` (`QuickSType.UI.csproj:9`) ✓ which is the prerequisite for trimming-friendly XAML.
- **`CommunityToolkit.Mvvm 8.4.2`** — uses source generators for `[ObservableProperty]` etc., AOT-safe ✓.
- **`Whisper.net 1.9.0`** — wraps native whisper.cpp; whether the managed shim is fully AOT-safe is **unverified** (Whisper.net does ship `[DynamicallyAccessedMembers]` annotations in recent versions but trim warnings have been reported). **Severity: Medium**.
- **`SharpHook 7.1.1`** — uses native libuiohook. The managed surface is well-trimmed; `TaskPoolGlobalHook` is AOT-friendly. **Severity: Low**.
- **`PortAudioSharp2 1.0.6`** — uses `[StructLayout]` + `[DllImport]` style. Likely produces AOT trimmer warnings; not blocking but noisy. **Severity: Low**.
- **`TextCopy 6.2.1`** — small library; mixed `DllImport` shape. **Severity: Low**.

Summary: AOT story is good. The likely failure points under a `dotnet publish -p:PublishAot=true` are the third-party native wrappers (Whisper.net, PortAudioSharp2, TextCopy) emitting trim warnings, not first-party code.

## Dependencies / supply chain

| Package | Version | Notes | Severity |
|---|---|---|---|
| `Avalonia*` | `11.2.3` | Current is 11.x stable line; 11.2.3 (released ~Jan 2025) is older than the latest 11.x patch. Consider `11.3.x`. | **Low** |
| `CommunityToolkit.Mvvm` | `8.4.2` | Recent and stable. | OK |
| `Microsoft.Extensions.Logging` | `10.0.0` | Tracks .NET 10 (matches `TargetFramework=net10.0` in `Directory.Build.props:5`). ✓ | OK |
| `Whisper.net` / `Whisper.net.Runtime` | `1.9.0` | Active project. Native binaries: ggml + whisper.cpp shipped as RID-specific NuGets. Bundled `ggml-metal.metal` is the source of the irrelevant TODO matches. | **Medium** — supply-chain risk is the main concern; the runtime NuGet pulls platform-native blobs that the consumer cannot easily verify. Mitigation: pin SHA in `packages.lock.json` (which is **not present** in this repo — `RestorePackagesWithLockFile` is unset). |
| `Whisper.net.Runtime.CoreML` | `1.9.0` (osx) | Conditional on macOS ✓. | OK |
| `Whisper.net.Runtime.Cuda.Windows` | `1.9.0` | Conditional on Windows ✓. CUDA runtime alone is huge (~hundreds MB) — published Windows binary will be heavy. | **Low** |
| `org.k2fsa.portaudio.runtime.osx-arm64` | `1.0.6` | k2fsa is a small org; verify trust posture. Native PortAudio binary. | **Medium** |
| `org.k2fsa.portaudio.runtime.osx-x64` | `1.0.6` | Same. | **Medium** |
| `org.k2fsa.portaudio.runtime.win-x64` | `1.0.6` | Same. | **Medium** |
| `PortAudioSharp2` | `1.0.6` | Managed P/Invoke shim. Same publisher. | **Medium** |
| `SharpHook` | `7.1.1` | Active project, well-maintained. | OK |
| `TextCopy` | `6.2.1` | Maintained by CopyText/CSharpCodeReview/SimonCropp; widely used. Note: `TextCopy.ClipboardService` static API is convenience-driven; on Mac it ultimately shells out to `osascript`/`pbpaste`. | **Low** |
| Test packages | `Microsoft.NET.Test.Sdk 17.12.0`, `xunit 2.9.2`, `xunit.runner.visualstudio 3.0.1`, `Shouldly 4.3.0` | All current-ish. xUnit v3 is out; on v2 line is fine. | OK |

Other supply-chain notes:

- **No `packages.lock.json`** anywhere (no `RestorePackagesWithLockFile=true` in `Directory.Build.props`). NuGet restore is therefore non-deterministic across machines and CI; transitive versions can float. Severity: **Medium**.
- **No `dependabot.yml` / `renovate.json` / vulnerability scan config** in repo root. Severity: **Low**.
- **`<NoWarn>$(NoWarn);CA1416;NU1701</NoWarn>`** in `Directory.Build.props:9` suppresses platform-compatibility warnings (CA1416) and "package was restored using a different framework" (NU1701) globally. These are *exactly* the warnings that would tell the developer about cross-platform parity bugs (the WindowsAutoLaunch CA1416 disable inside the file at lines 13/16 etc. is now redundant). Severity: **Medium** — silences signal that would catch real issues.
- **`build/` directory exists** in the repo root (sibling to `src/`) — not inspected here, but worth noting it's not gitignored and may carry build artifacts.

---

## Top-3 things I would fix first

1. **Add SHA-256 verification to `ModelDownloader`** (`ModelDownloader.cs`, `ModelCatalog.cs`). High-impact security + correctness; both the `206`-vs-`200` resume bug and the supply-chain risk are mitigated by the same patch.
2. **Fix the rapid press-release-press race in `DictationEngine.OnHotkeyReleased`** (`DictationEngine.cs:86-101`) by serialising `ProcessAsync` invocations or by guarding `PasteAsync` against the cancelled CTS finishing first.
3. **Detect and surface "AX permission missing" in `MacPasteService`** before clobbering the clipboard (`MacPasteService.cs:20-53`). The current failure mode silently destroys the user's clipboard with no paste — the worst possible UX for a paste tool.
