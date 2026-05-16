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
  if repo="$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null)"; then
    :
  else
    repo="$(git remote get-url origin | sed -E 's#^https://github.com/##; s#^git@github.com:##; s#\.git$##')"
  fi
fi

if [[ -z "$evidence_output" ]]; then
  evidence_output=".planning/release-evidence-v${version}.md"
fi

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

echo "Running stable v$version release for $repo"
bash "$script_dir/run-stable-release.sh" "$version" "$repo" "$evidence_output"

echo "Generating distribution manifests from $evidence_output"
bash "$repo_root/distribution/generate-from-release-evidence.sh" "$version" "$evidence_output" "$repo"

echo "v$version release evidence and distribution manifests are ready."
echo "Next: validate Homebrew/Winget installs and rerun build/audit-release-readiness.sh $repo"
