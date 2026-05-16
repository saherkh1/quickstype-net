#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
url="${2:-}"
sha256="${3:-}"
output="${4:-distribution/homebrew/quickstype.rb}"

if [[ -z "$version" || -z "$url" || -z "$sha256" ]]; then
  echo "Usage: $0 <version> <pkg-url> <sha256> [output]" >&2
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

if [[ ! "$url" =~ ^https://github[.]com/[^/]+/[^/]+/releases/download/v([0-9A-Za-z.-]+)/QuickSType-[^/]+-Setup[.]pkg$ ]]; then
  echo "Homebrew package URL must be a GitHub release QuickSType .pkg asset URL." >&2
  exit 64
fi

if [[ "${BASH_REMATCH[1]}" != "$version" ]]; then
  echo "Homebrew package URL release tag must match version v$version." >&2
  exit 64
fi

mkdir -p "$(dirname "$output")"
pkg_name="$(basename "${url%%\?*}")"
cat > "$output" <<CASK
cask "quickstype" do
  version "$version"
  sha256 "$sha256"

  url "$url"
  name "QuickSType"
  desc "Local-first push-to-talk dictation for macOS and Windows"
  homepage "https://github.com/saherkh1/quickstype-net"

  pkg "$pkg_name"

  uninstall pkgutil: "com.saherk.quickstype"

  zap trash: [
    "~/Library/Application Support/QuickSType",
    "~/Library/Caches/QuickSType",
    "~/Library/Logs/QuickSType",
    "~/Library/Preferences/com.saherk.quickstype.plist",
  ]
end
CASK

echo "Wrote $output"
