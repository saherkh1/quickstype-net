#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
repo="${2:-}"
output="${3:-}"

if [[ -z "$version" ]]; then
  echo "Usage: $0 <version> [owner/repo] [output.md]" >&2
  echo "Example: $0 0.99.0 saherkh1/quickstype-net .planning/release-evidence-v0.99.0.md" >&2
  exit 64
fi

if [[ ! "$version" =~ ^v?[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "Version must be SemVer with or without a leading v: $version" >&2
  exit 64
fi

if [[ -z "$repo" ]]; then
  repo="$(gh repo view --json nameWithOwner -q .nameWithOwner)"
fi

tag="$version"
if [[ "$tag" != v* ]]; then
  tag="v$tag"
fi

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command not found: $command_name" >&2
    exit 1
  fi
}

release_field() {
  local field="$1"
  gh release view "$tag" --repo "$repo" --json "$field" -q ".$field"
}

emit() {
  if [[ -n "$output" ]]; then
    printf '%s\n' "$1" >>"$output"
  else
    printf '%s\n' "$1"
  fi
}

require_command gh
require_command shasum
require_command mktemp
require_command find
require_command sort

if ! gh release view "$tag" --repo "$repo" >/dev/null 2>&1; then
  echo "Release $tag does not exist in $repo." >&2
  exit 1
fi

tmpdir="$(mktemp -d)"
cleanup() {
  rm -rf "$tmpdir"
}
trap cleanup EXIT

gh release download "$tag" --repo "$repo" --dir "$tmpdir" --clobber

if [[ -n "$output" ]]; then
  mkdir -p "$(dirname "$output")"
  : >"$output"
fi

release_url="$(release_field url)"
published_at="$(release_field publishedAt)"
is_prerelease="$(release_field isPrerelease)"

latest_run="$(gh run list \
  --repo "$repo" \
  --workflow release.yml \
  --limit 20 \
  --json databaseId,conclusion,createdAt,event,status,url \
  -q 'map(select(.event == "workflow_dispatch" or .event == "push")) | .[0] // empty')"

emit "# QuickSType Release Evidence - $tag"
emit ""
emit "| Field | Value |"
emit "|-------|-------|"
emit "| Repository | $repo |"
emit "| Tag | $tag |"
emit "| Release URL | $release_url |"
emit "| Published at | $published_at |"
emit "| Prerelease | $is_prerelease |"

if [[ -n "$latest_run" ]]; then
  run_url="$(gh run list --repo "$repo" --workflow release.yml --limit 20 --json url -q '.[0].url')"
  run_status="$(gh run list --repo "$repo" --workflow release.yml --limit 20 --json status -q '.[0].status')"
  run_conclusion="$(gh run list --repo "$repo" --workflow release.yml --limit 20 --json conclusion -q '.[0].conclusion')"
  emit "| Latest release workflow run | $run_url ($run_status/$run_conclusion) |"
fi

emit ""
emit "## Asset Checksums"
emit ""
emit "| Asset | Size bytes | SHA-256 |"
emit "|-------|------------|---------|"

while IFS= read -r file; do
  name="$(basename "$file")"
  size="$(wc -c <"$file" | tr -d ' ')"
  sha256="$(shasum -a 256 "$file" | awk '{print $1}')"
  emit "| $name | $size | $sha256 |"
done < <(find "$tmpdir" -type f | sort)

emit ""
emit "## Manual Sign-Off"
emit ""
emit "- [ ] macOS package signing identity matches expected Developer ID Application/Installer identities."
emit "- [ ] macOS notarization and stapler validation passed in the release workflow."
emit "- [ ] Windows Azure Artifact Signing completed for publish directory and installer."
emit "- [ ] `tests/manual/UPDATE_CANARY_MATRIX.md` rows updated with tester/date and PASS/DEFERRED status."
emit "- [ ] Homebrew and Winget manifest generators use the exact URLs and SHA-256 values above."

if [[ -n "$output" ]]; then
  echo "Wrote release evidence to $output"
fi
