#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
evidence="${2:-}"
repo="${3:-saherkh1/quickstype-net}"

if [[ -z "$version" || -z "$evidence" ]]; then
  echo "Usage: $0 <version> <release-evidence.md> [owner/repo]" >&2
  exit 64
fi

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "Version must be SemVer without a leading v: $version" >&2
  exit 64
fi

if [[ ! -f "$evidence" ]]; then
  echo "Release evidence file does not exist: $evidence" >&2
  exit 1
fi

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
tag="v$version"

bash "$repo_root/build/validate-release-evidence.sh" "$evidence" false "$tag" >/dev/null

extract_asset_field() {
  local pattern="$1"
  local field="$2"

  awk -F'|' -v pattern="$pattern" -v field="$field" '
    /^\|/ {
      asset = $2
      sha = $4
      gsub(/^[ \t]+|[ \t]+$/, "", asset)
      gsub(/^[ \t]+|[ \t]+$/, "", sha)
      if (asset ~ pattern && sha ~ /^[a-fA-F0-9]{64}$/) {
        if (field == "asset") print asset
        if (field == "sha") print sha
        exit
      }
    }
  ' "$evidence"
}

base_url="https://github.com/${repo}/releases/download/${tag}"

mac_asset="$(extract_asset_field 'QuickSType-.*-Setup[.]pkg$' asset)"
mac_sha="$(extract_asset_field 'QuickSType-.*-Setup[.]pkg$' sha)"
win_asset="$(extract_asset_field 'QuickSType-.*-Setup[.]exe$' asset)"
win_sha="$(extract_asset_field 'QuickSType-.*-Setup[.]exe$' sha)"

if [[ -z "$mac_asset" || -z "$mac_sha" ]]; then
  echo "Could not find macOS .pkg setup asset with SHA-256 in $evidence" >&2
  exit 1
fi

if [[ -z "$win_asset" || -z "$win_sha" ]]; then
  echo "Could not find Windows .exe setup asset with SHA-256 in $evidence" >&2
  exit 1
fi

mac_url="${base_url}/${mac_asset}"
win_url="${base_url}/${win_asset}"

bash "$script_dir/homebrew/generate-cask.sh" "$version" "$mac_url" "$mac_sha"
bash "$script_dir/winget/generate-winget-manifests.sh" "$version" "$win_url" "$win_sha"

echo "Generated distribution manifests from $evidence"
