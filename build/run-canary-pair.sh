#!/usr/bin/env bash
set -euo pipefail

repo="${1:-}"
first_version="${2:-0.99.0}"
second_version="${3:-0.99.1}"

if [[ -z "$repo" ]]; then
  if repo="$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null)"; then
    :
  else
    repo="$(git remote get-url origin | sed -E 's#^https://github.com/##; s#^git@github.com:##; s#\.git$##')"
  fi
fi

for version in "$first_version" "$second_version"; do
  if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
    echo "Version must be SemVer without a leading v: $version" >&2
    exit 64
  fi
done

if [[ "$first_version" == "$second_version" ]]; then
  echo "Canary versions must be different." >&2
  exit 64
fi

script_dir="$(cd "$(dirname "$0")" && pwd)"

for version in "$first_version" "$second_version"; do
  evidence_output=".planning/release-evidence-v${version}.md"

  echo "Running canary release v$version for $repo"
  bash "$script_dir/run-canary-release.sh" "$version" canary "$repo"

  echo "Collecting canary release evidence for v$version"
  bash "$script_dir/collect-release-evidence.sh" "$version" "$repo" "$evidence_output"
  echo "Canary evidence written to $evidence_output"
done

echo "Canary pair complete. Install v$first_version, update to v$second_version, then complete tests/manual/UPDATE_CANARY_MATRIX.md."
