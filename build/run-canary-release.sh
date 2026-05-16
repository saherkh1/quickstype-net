#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
channel="${2:-canary}"
repo="${3:-}"
run_id_output="${4:-}"

if [[ -z "$version" ]]; then
  echo "Usage: $0 <version> [channel] [owner/repo] [run-id-output]" >&2
  echo "Example: $0 0.99.0 canary saherkh1/quickstype-net /tmp/quickstype-release-run-id" >&2
  exit 64
fi

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "Version must be SemVer without a leading v: $version" >&2
  exit 64
fi

if [[ "$channel" != "canary" && "$channel" != "stable" ]]; then
  echo "Channel must be canary or stable: $channel" >&2
  exit 64
fi

if [[ -z "$repo" ]]; then
  repo="$(gh repo view --json nameWithOwner -q .nameWithOwner)"
fi

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Working tree is dirty. Commit or stash changes before dispatching a release." >&2
  exit 1
fi

bash "$(dirname "$0")/check-release-prereqs.sh" "$repo"

branch="$(git branch --show-current)"
head_sha="$(git rev-parse HEAD)"
remote_sha="$(git ls-remote "https://github.com/${repo}.git" "refs/heads/${branch}" | awk '{print $1}')"

if [[ "$head_sha" != "$remote_sha" ]]; then
  echo "Local HEAD $head_sha does not match $repo/$branch $remote_sha." >&2
  echo "Push or pull before dispatching a release." >&2
  exit 1
fi

echo "Dispatching release workflow for $repo version=$version channel=$channel"
gh workflow run release.yml \
  --repo "$repo" \
  --ref "$branch" \
  -f "version=$version" \
  -f "channel=$channel"

echo "Waiting for release workflow run to appear..."
run_id=""
for _ in {1..20}; do
  run_id="$(gh run list \
    --repo "$repo" \
    --workflow release.yml \
    --branch "$branch" \
    --limit 1 \
    --json databaseId,event,headSha,status \
    -q ".[] | select(.event == \"workflow_dispatch\" and .headSha == \"$head_sha\") | .databaseId" || true)"
  if [[ -n "$run_id" ]]; then
    break
  fi
  sleep 3
done

if [[ -z "$run_id" ]]; then
  echo "Could not find the dispatched release workflow run." >&2
  exit 1
fi

echo "Watching release workflow run $run_id"
gh run watch "$run_id" --repo "$repo" --exit-status

if [[ -n "$run_id_output" ]]; then
  mkdir -p "$(dirname "$run_id_output")"
  printf '%s\n' "$run_id" >"$run_id_output"
  echo "Wrote release workflow run id to $run_id_output"
fi
