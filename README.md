# QuickSType

Cross-platform push-to-talk voice dictation for macOS and Windows. Hold a hotkey, speak, release — the transcribed text appears at your cursor. 100% local, zero cloud.

> Successor to the Python POC at `mac-quickStype` (frozen at v0.1.0-poc).

## Features

- Hold-to-talk global hotkey (default: Right Option / Right Alt)
- Local transcription via [whisper.cpp](https://github.com/ggerganov/whisper.cpp) through [Whisper.net](https://github.com/sandrohanea/whisper.net) — Metal on Mac, CUDA on Windows
- Tray / menu-bar icon with three states (idle / recording / processing)
- Quick language switch from the tray menu (English / Hebrew / Arabic / Auto-detect / ...)
- First-run wizard: mic permission, model download, mic test
- Settings window: model picker, hotkey rebind, audio device, languages, start-on-login
- Single AOT-compiled binary per platform (~40–60 MB)

## Platforms

- macOS 13+ (Apple Silicon priority; Intel works)
- Windows 10/11 x64

## Project Layout

```
src/
  QuickSType.Core/                pure cross-platform logic
  QuickSType.Platform.Mac/        Mac P/Invoke adapters
  QuickSType.Platform.Windows/    Windows P/Invoke adapters
  QuickSType.UI/                  Avalonia app (entry point)
tests/
  QuickSType.Core.Tests/          xunit
build/
  publish-mac.sh                  wraps publish output into .app
  publish-windows.ps1
docs/
  ARCHITECTURE.md
```

## Build

Requires .NET 9 SDK.

```bash
dotnet restore
dotnet build
dotnet test
```

Run the UI:

```bash
dotnet run --project src/QuickSType.UI
```

## Publish (AOT)

```bash
# macOS Apple Silicon
./build/publish-mac.sh osx-arm64

# macOS Intel
./build/publish-mac.sh osx-x64

# Windows
pwsh build/publish-windows.ps1
```

Outputs:
- `publish/QuickSType.app` (Mac)
- `publish/win-x64/QuickSType.exe` (Windows)

## Status

In active development. See `docs/ARCHITECTURE.md` for the design and phasing.

## License

MIT — © 2026 Saher Khateeb
