#!/usr/bin/env bash
set -euo pipefail

version="${1:-1.0.0}"
repo="${2:-}"
evidence_output="${3:-}"

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Version must be stable SemVer without a leading v: $version" >&2
  exit 64
fi

if [[ -z "$repo" ]]; then
  repo="$(gh repo view --json nameWithOwner -q .nameWithOwner)"
fi

if [[ -z "$evidence_output" ]]; then
  evidence_output=".planning/release-evidence-v${version}.md"
fi

signing_mode="${QUICKSTYPE_RELEASE_SIGNING_MODE:-unsigned}"
if [[ "$signing_mode" != "signed" && "$signing_mode" != "unsigned" ]]; then
  echo "QUICKSTYPE_RELEASE_SIGNING_MODE must be signed or unsigned: $signing_mode" >&2
  exit 64
fi

tmpdir="$(mktemp -d)"
cleanup() {
  rm -rf "$tmpdir"
}
trap cleanup EXIT
run_id_file="$tmpdir/release-run-id"

echo "Dispatching stable release v$version for $repo signing_mode=$signing_mode"
QUICKSTYPE_RELEASE_SIGNING_MODE="$signing_mode" bash "$(dirname "$0")/run-canary-release.sh" "$version" stable "$repo" "$run_id_file"

echo "Collecting stable release evidence for v$version"
QUICKSTYPE_RELEASE_SIGNING_MODE="$signing_mode" bash "$(dirname "$0")/collect-release-evidence.sh" "$version" "$repo" "$evidence_output" "$(cat "$run_id_file")"

echo "Stable release evidence written to $evidence_output"
