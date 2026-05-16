# Distribution Manifests

These helpers prepare Homebrew and WinGet manifest files after Phase 6 produces signed release artifacts.

Do not publish generated manifests until:

- macOS artifact is signed and notarized.
- Windows artifact is signed.
- SHA-256 values are computed from the exact uploaded release assets.
- `tests/manual/UPDATE_CANARY_MATRIX.md` records the canary update evidence.

## Homebrew

```bash
bash distribution/homebrew/generate-cask.sh \
  1.0.0 \
  https://github.com/saherkh1/quickstype-net/releases/download/v1.0.0/QuickSType-canary-Setup.pkg \
  <sha256>
```

Then copy `distribution/homebrew/quickstype.rb` into the tap and run:

```bash
brew audit --cask --online quickstype
brew install --cask saherk/quickstype/quickstype
```

## WinGet

```bash
bash distribution/winget/generate-winget-manifests.sh \
  1.0.0 \
  https://github.com/saherkh1/quickstype-net/releases/download/v1.0.0/QuickSType-canary-Setup.exe \
  <sha256>
```

Then validate on Windows:

```powershell
winget validate distribution\winget\manifests\q\QuickSType\QuickSType\1.0.0
```
