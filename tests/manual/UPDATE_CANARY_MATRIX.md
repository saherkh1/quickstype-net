# QuickSType Update Canary Matrix

Phase: 06 Auto-Update
Canary path: v0.99.0 to v0.99.1
Status: PENDING

This matrix must be completed with installed, signed/notarized release artifacts from `.github/workflows/release.yml`.

| ID | Platform | Scenario | Old version | New version | Install path | Update source | Expected result | Status | Tester/date | Notes |
|----|----------|----------|-------------|-------------|--------------|---------------|-----------------|--------|-------------|-------|
| UPDATE-CANARY-01 | macOS arm64 | Install v0.99.0, update in-app to v0.99.1 | v0.99.0 | v0.99.1 | `/Applications/QuickSType.app` | GitHub Releases canary feed | App updates and relaunches as v0.99.1 | PENDING | - | Requires signed/notarized artifacts |
| UPDATE-CANARY-02 | macOS arm64 | Accessibility granted before update | v0.99.0 | v0.99.1 | `/Applications/QuickSType.app` | GitHub Releases canary feed | No Accessibility permission re-prompt; permission remains trusted | PENDING | - | Requires physical macOS test |
| UPDATE-CANARY-03 | macOS arm64 | Accessibility missing after update | v0.99.0 | v0.99.1 | `/Applications/QuickSType.app` | GitHub Releases canary feed | General-tab banner appears with Open System Settings action | PENDING | - | Revoke permission before/after update |
| UPDATE-CANARY-04 | Windows x64 | Install v0.99.0, update in-app to v0.99.1 | v0.99.0 | v0.99.1 | Stable Velopack install directory | GitHub Releases canary feed | App updates and relaunches as v0.99.1 | PENDING | - | Requires signed Azure Artifact Signing installer |
| UPDATE-CANARY-05 | Windows x64 | Winget-managed marker/config | v0.99.0 | n/a | Winget install path | n/a | UI says updates managed by Winget; no in-app update prompt | PENDING | - | Can use config `managed_package_manager=winget` until real Winget |
| UPDATE-CANARY-06 | macOS arm64 | Homebrew-managed marker/config | v0.99.0 | n/a | Homebrew cask app path | n/a | UI says updates managed by Homebrew; no in-app update prompt | PENDING | - | Can use config `managed_package_manager=homebrew` until real cask |

## Required Release Evidence

Record these for both `v0.99.0` and `v0.99.1`:

- Git tag
- GitHub Release URL
- Workflow run URL
- macOS package artifact names and SHA-256
- Windows package artifact names and SHA-256
- macOS signing identity
- macOS notarization status
- Windows Artifact Signing status
- `releases.canary.json` checksum

Use the evidence collector after each release workflow succeeds:

```bash
bash build/collect-release-evidence.sh 0.99.0 saherkh1/quickstype-net .planning/release-evidence-v0.99.0.md
bash build/collect-release-evidence.sh 0.99.1 saherkh1/quickstype-net .planning/release-evidence-v0.99.1.md
```

## Closure Rule

Phase 6 can close only when every row is `PASS`, or when any remaining row is explicitly marked `DEFERRED` with owner/date and accepted release risk.
