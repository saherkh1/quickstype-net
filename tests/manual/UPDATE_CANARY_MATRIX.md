# QuickSType Update Canary Matrix

Phase: 06 Auto-Update
Canary path: v0.99.0 to v0.99.1
Status: PENDING

This matrix must be completed with installed, signed/notarized release artifacts from `.github/workflows/release.yml`.

## Operator Prerequisites

Before marking any row `PASS`:

- `bash build/check-release-prereqs.sh saherkh1/quickstype-net` passes.
- `bash build/run-canary-pair.sh saherkh1/quickstype-net` passes.
- `.planning/release-evidence-v0.99.0.md` and `.planning/release-evidence-v0.99.1.md` exist.
- The tester records OS version, CPU architecture, installed artifact name, SHA-256, and GitHub Release URL in Notes.

## Config Locations

Update behavior is controlled by `AppConfig`:

- macOS: `~/Library/Application Support/QuickSType/config.json`
- Windows: `%APPDATA%\QuickSType\config.json`

For in-app canary update scenarios, quit QuickSType and ensure:

```json
{
  "update_source_url": "https://github.com/saherkh1/quickstype-net",
  "update_channel": "canary",
  "managed_package_manager": null,
  "enable_background_update_checks": true
}
```

For package-manager-managed scenarios, set `managed_package_manager` to `homebrew` or `winget`. The UI must show `Updates managed by Homebrew` or `Updates managed by Winget`, and the update action must be disabled.

## Scenario Procedure

1. Install `v0.99.0` from the signed release asset for the target platform.
2. Launch QuickSType and confirm the Settings Updates section reports current version `0.99.0` or the Velopack-installed equivalent.
3. Confirm the configured channel is `canary`.
4. Run the scenario-specific setup from the table row.
5. Use Settings or the tray menu to check for updates.
6. If an update is available, download it and choose `Install and Restart`.
7. After relaunch, confirm version `0.99.1`, update source, install path, and the expected row-specific behavior.
8. Record tester/date and notes before changing Status.

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

Use the evidence collector after each release workflow succeeds, or run `build/run-canary-pair.sh` to dispatch both canaries and collect both evidence files:

```bash
bash build/run-canary-pair.sh saherkh1/quickstype-net

bash build/collect-release-evidence.sh 0.99.0 saherkh1/quickstype-net .planning/release-evidence-v0.99.0.md
bash build/collect-release-evidence.sh 0.99.1 saherkh1/quickstype-net .planning/release-evidence-v0.99.1.md
```

## Closure Rule

Phase 6 can close only when every row is `PASS`, or when any remaining row is explicitly marked `DEFERRED` with owner/date and accepted release risk.
