#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
installer_url="${2:-}"
sha256="${3:-}"
root="${4:-distribution/winget/manifests/q/QuickSType/QuickSType}"

if [[ -z "$version" || -z "$installer_url" || -z "$sha256" ]]; then
  echo "Usage: $0 <version> <installer-url> <sha256> [manifest-root]" >&2
  exit 64
fi

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "Version must be SemVer without a leading v: $version" >&2
  exit 64
fi

if [[ ! "$sha256" =~ ^[a-fA-F0-9]{64}$ ]]; then
  echo "SHA-256 must be a 64-character hex string." >&2
  exit 64
fi

manifest_dir="$root/$version"
mkdir -p "$manifest_dir"
sha256_upper="$(printf '%s' "$sha256" | tr '[:lower:]' '[:upper:]')"

cat > "$manifest_dir/QuickSType.QuickSType.yaml" <<YAML
# Created with komac v2.13.0
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.version.1.10.0.schema.json

PackageIdentifier: QuickSType.QuickSType
PackageVersion: $version
DefaultLocale: en-US
ManifestType: version
ManifestVersion: 1.10.0
YAML

cat > "$manifest_dir/QuickSType.QuickSType.locale.en-US.yaml" <<YAML
# Created with komac v2.13.0
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.defaultLocale.1.10.0.schema.json

PackageIdentifier: QuickSType.QuickSType
PackageVersion: $version
PackageLocale: en-US
Publisher: Saher Khateeb
PackageName: QuickSType
License: MIT
ShortDescription: Local-first push-to-talk dictation for macOS and Windows.
Description: QuickSType is a local-first push-to-talk dictation app that streams speech into the focused text field using local Whisper transcription.
Homepage: https://github.com/saherkh1/quickstype-net
Repository: https://github.com/saherkh1/quickstype-net
Tags:
- dictation
- whisper
- speech-to-text
- local-first
ManifestType: defaultLocale
ManifestVersion: 1.10.0
YAML

cat > "$manifest_dir/QuickSType.QuickSType.installer.yaml" <<YAML
# Created with komac v2.13.0
# yaml-language-server: \$schema=https://aka.ms/winget-manifest.installer.1.10.0.schema.json

PackageIdentifier: QuickSType.QuickSType
PackageVersion: $version
InstallerLocale: en-US
InstallerType: exe
Scope: user
InstallModes:
- interactive
- silent
- silentWithProgress
UpgradeBehavior: install
ReleaseDate: $(date -u +%Y-%m-%d)
Installers:
- Architecture: x64
  InstallerUrl: $installer_url
  InstallerSha256: $sha256_upper
ManifestType: installer
ManifestVersion: 1.10.0
YAML

echo "Wrote $manifest_dir"
