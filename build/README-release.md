# QuickSType Release Workflow

Phase 6 uses `.github/workflows/release.yml` for canary and later stable release artifacts.

## Trigger

- Push a `v*` tag, for example `v0.99.0`.
- Or run the workflow manually and provide `version` and `channel`.

Tag pushes default to the `canary` Velopack channel during Phase 6.

## macOS Secrets

Required repository secrets:

- `APPLE_DEVELOPER_ID_APPLICATION_P12_BASE64`: base64 encoded Developer ID Application `.p12`.
- `APPLE_DEVELOPER_ID_APPLICATION_PASSWORD`: password for that `.p12`.
- `APPLE_DEVELOPER_ID_INSTALLER_P12_BASE64`: base64 encoded Developer ID Installer `.p12`.
- `APPLE_DEVELOPER_ID_INSTALLER_PASSWORD`: password for that `.p12`.
- `APPLE_KEYCHAIN_PASSWORD`: temporary CI keychain password.
- `APPLE_DEVELOPER_ID_APPLICATION_IDENTITY`: codesign subject, for example `Developer ID Application: Name (TEAMID)`.
- `APPLE_DEVELOPER_ID_INSTALLER_IDENTITY`: productsign subject, for example `Developer ID Installer: Name (TEAMID)`.
- `APPLE_ID`: Apple ID email used for notarization.
- `APPLE_TEAM_ID`: Apple Developer Team ID.
- `APPLE_APP_SPECIFIC_PASSWORD`: app-specific password for `notarytool`.

The workflow imports both certificates into a temporary keychain, stores a `notarytool` profile named `quickstype-notary`, and calls:

```bash
vpk pack \
  --signAppIdentity "$APPLE_DEVELOPER_ID_APPLICATION_IDENTITY" \
  --signInstallIdentity "$APPLE_DEVELOPER_ID_INSTALLER_IDENTITY" \
  --signEntitlements build/QuickSType.entitlements \
  --notaryProfile quickstype-notary \
  --keychain "$KEYCHAIN_PATH"
```

Velopack performs app signing, package creation, notarization, and stapling. The workflow also runs `xcrun stapler validate` against the generated installer package.

## Windows Azure Artifact Signing

Required repository secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_ARTIFACT_SIGNING_ENDPOINT`, for example `https://eus.codesigning.azure.net/`
- `AZURE_ARTIFACT_SIGNING_ACCOUNT`
- `AZURE_ARTIFACT_SIGNING_CERT_PROFILE`

The service principal or federated credential must be allowed to sign with the Artifact Signing certificate profile. The workflow signs:

1. The AOT publish directory before Velopack packing (`exe,dll`, recursive).
2. The generated Velopack installer (`exe`) after packing.

Timestamping uses `http://timestamp.acs.microsoft.com` with SHA-256.

## Local Preconditions

- `vpk` is installed in the workflow with `dotnet tool install --tool-path .tools vpk --version 0.0.1298`.
- `DOTNET_ROLL_FORWARD=Major` is set because the current Velopack CLI targets .NET 9 and the repo uses .NET 10.
- Release artifacts remain under ignored `artifacts/`.

## Remote Prerequisite Check

After committing and pushing the workflow changes, check that GitHub can see the workflows and required secret names:

```bash
bash build/check-release-prereqs.sh saherkh1/quickstype-net
```

The script checks workflow names and secret names only; it does not read secret values.

## Configure Repository Secrets

Use `build/configure-release-secrets.sh` to upload the required GitHub repository secrets from local environment variables. The helper does not print secret values. For Apple `.p12` certificates, either provide a pre-encoded `*_P12_BASE64` value or a local `*_P12_PATH` file path; the helper will base64 encode the file before uploading it.

```bash
export APPLE_DEVELOPER_ID_APPLICATION_P12_PATH=/path/to/developer-id-application.p12
export APPLE_DEVELOPER_ID_APPLICATION_PASSWORD='...'
export APPLE_DEVELOPER_ID_INSTALLER_P12_PATH=/path/to/developer-id-installer.p12
export APPLE_DEVELOPER_ID_INSTALLER_PASSWORD='...'
export APPLE_KEYCHAIN_PASSWORD='...'
export APPLE_DEVELOPER_ID_APPLICATION_IDENTITY='Developer ID Application: Example (TEAMID)'
export APPLE_DEVELOPER_ID_INSTALLER_IDENTITY='Developer ID Installer: Example (TEAMID)'
export APPLE_ID='apple-id@example.com'
export APPLE_TEAM_ID='TEAMID'
export APPLE_APP_SPECIFIC_PASSWORD='...'

export AZURE_CLIENT_ID='...'
export AZURE_TENANT_ID='...'
export AZURE_SUBSCRIPTION_ID='...'
export AZURE_ARTIFACT_SIGNING_ENDPOINT='https://eus.codesigning.azure.net/'
export AZURE_ARTIFACT_SIGNING_ACCOUNT='...'
export AZURE_ARTIFACT_SIGNING_CERT_PROFILE='...'

bash build/configure-release-secrets.sh saherkh1/quickstype-net
```

The helper uploads each secret with `gh secret set`, then runs `build/check-release-prereqs.sh` so missing names are caught immediately.

## Canary Dispatch

After the prerequisite check passes, dispatch and watch a canary release:

```bash
bash build/run-canary-release.sh 0.99.0 canary saherkh1/quickstype-net
bash build/run-canary-release.sh 0.99.1 canary saherkh1/quickstype-net
```

The helper refuses to run with a dirty working tree, verifies local `HEAD` matches the remote branch, checks workflows/secrets, dispatches `release.yml`, and watches the GitHub Actions run.

## Expected Release Assets

Each platform upload should add the Velopack package output for the selected channel, including:

- `releases.canary.json`
- `assets.canary.json`
- `RELEASES-canary`
- platform full package
- platform installer/portable artifact

The workflow uses `vpk upload github --merge --publish --pre` so macOS and Windows jobs can attach to the same GitHub Release.

## Collect Release Evidence

After a canary or stable release workflow passes, collect the release URL, workflow run pointer, asset list, byte sizes, and SHA-256 checksums:

```bash
bash build/collect-release-evidence.sh 0.99.0 saherkh1/quickstype-net .planning/release-evidence-v0.99.0.md
bash build/collect-release-evidence.sh 0.99.1 saherkh1/quickstype-net .planning/release-evidence-v0.99.1.md
```

Use the generated evidence files to fill `tests/manual/UPDATE_CANARY_MATRIX.md` and to pass exact uploaded asset URLs/checksums into the Homebrew and Winget manifest generators.

## Current Caveats

- The workflow has not been run against real Apple/Azure credentials yet.
- The workflow must be committed and pushed before it appears in `gh workflow list`.
- Local macOS AOT packaging still fails on machines missing OpenSSL linker libraries; CI release remains the authority for AOT release proof.
- Phase 6 cannot be closed until `v0.99.0` and `v0.99.1` canary updates are verified in `tests/manual/UPDATE_CANARY_MATRIX.md`.
