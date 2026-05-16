#!/usr/bin/env bash
set -euo pipefail

repo="${1:-}"
if [[ -z "$repo" ]]; then
  if repo="$(gh repo view --json nameWithOwner -q .nameWithOwner 2>/dev/null)"; then
    :
  else
    repo="$(git remote get-url origin | sed -E 's#^https://github.com/##; s#^git@github.com:##; s#\.git$##')"
  fi
fi

failures=0

pass() {
  echo "PASS: $1"
}

fail() {
  echo "FAIL: $1"
  failures=$((failures + 1))
}

warn() {
  echo "WARN: $1"
}

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    fail "Required command not found: $command_name"
    return 1
  fi
}

check_release() {
  local tag="$1"
  if gh release view "$tag" --repo "$repo" >/dev/null 2>&1; then
    pass "GitHub Release $tag exists"
  else
    fail "GitHub Release $tag is missing"
  fi
}

check_release_evidence() {
  local evidence="$1"
  local expected_prerelease="$2"
  local expected_tag="$3"

  if bash "$(dirname "$0")/validate-release-evidence.sh" "$evidence" "$expected_prerelease" "$expected_tag" >/tmp/quickstype-release-evidence-validation.log 2>&1; then
    pass "Release evidence exists with completed manual sign-off and workflow proof: $evidence"
  else
    fail "$(cat /tmp/quickstype-release-evidence-validation.log)"
  fi
}

check_head_workflow_success() {
  local workflow_name="$1"
  local run

  run="$(gh run list \
    --repo "$repo" \
    --commit "$head_sha" \
    --workflow "$workflow_name" \
    --limit 1 \
    --json conclusion,status,url \
    -q '.[] | "\(.status)|\(.conclusion)|\(.url)"' 2>/dev/null || true)"

  if [[ -z "$run" ]]; then
    fail "Workflow $workflow_name has no run for local HEAD"
    return
  fi

  IFS='|' read -r status conclusion url <<<"$run"
  if [[ "$status" == "completed" && "$conclusion" == "success" ]]; then
    pass "Workflow $workflow_name succeeded for local HEAD ($url)"
  else
    fail "Workflow $workflow_name is not green for local HEAD (status=$status, conclusion=$conclusion, url=$url)"
  fi
}

echo "Auditing QuickSType release readiness for $repo"

require_command gh || true
require_command git || true
require_command awk || true

if [[ "$failures" -ne 0 ]]; then
  echo "Cannot continue audit until required commands are available." >&2
  exit 1
fi

if [[ -n "$(git status --porcelain)" ]]; then
  fail "Working tree is dirty"
else
  pass "Working tree is clean"
fi

branch="$(git branch --show-current)"
head_sha="$(git rev-parse HEAD)"
remote_sha="$(git ls-remote "https://github.com/${repo}.git" "refs/heads/${branch}" | awk '{print $1}')"
if [[ -z "$remote_sha" ]]; then
  fail "Remote branch $repo/$branch was not found"
elif [[ "$head_sha" == "$remote_sha" ]]; then
  pass "Local HEAD matches $repo/$branch"
else
  fail "Local HEAD $head_sha does not match $repo/$branch $remote_sha"
fi

for workflow_name in ci secret-scan; do
  check_head_workflow_success "$workflow_name"
done

if bash "$(dirname "$0")/check-release-prereqs.sh" "$repo" >/tmp/quickstype-release-prereqs.log 2>&1; then
  pass "Release workflows and signing secrets are configured"
else
  fail "Release workflows/signing secrets are incomplete"
  sed 's/^/  /' /tmp/quickstype-release-prereqs.log
  echo "  Local release secret env preflight:"
  if bash "$(dirname "$0")/configure-release-secrets.sh" --check-env "$repo" >/tmp/quickstype-release-env.log 2>&1; then
    sed 's/^/    /' /tmp/quickstype-release-env.log
    echo "    Next: bash $(dirname "$0")/configure-release-secrets.sh $repo"
  else
    sed 's/^/    /' /tmp/quickstype-release-env.log
    echo "    Next: export the missing values, then run bash $(dirname "$0")/configure-release-secrets.sh --check-env $repo"
  fi
fi

for tag in v0.99.0 v0.99.1 v1.0.0; do
  check_release "$tag"
done

injection_matrix="tests/manual/INJECTION_MATRIX.md"
update_matrix="tests/manual/UPDATE_CANARY_MATRIX.md"
telemetry_matrix="tests/manual/TELEMETRY_MATRIX.md"
for matrix_spec in \
  "$injection_matrix:injection:HUD injection matrix" \
  "$update_matrix:update:Canary update matrix" \
  "$telemetry_matrix:telemetry:Telemetry matrix"; do
  IFS=':' read -r matrix kind label <<<"$matrix_spec"
  if bash "$(dirname "$0")/validate-manual-matrix.sh" "$matrix" "$kind" >/tmp/quickstype-manual-matrix-validation.log 2>&1; then
    pass "$label is accepted with tester/date evidence"
  else
    fail "$(cat /tmp/quickstype-manual-matrix-validation.log)"
  fi
done

for evidence_spec in \
  .planning/release-evidence-v0.99.0.md:true:v0.99.0 \
  .planning/release-evidence-v0.99.1.md:true:v0.99.1 \
  .planning/release-evidence-v1.0.0.md:false:v1.0.0; do
  IFS=':' read -r evidence expected_prerelease expected_tag <<<"$evidence_spec"
  check_release_evidence "$evidence" "$expected_prerelease" "$expected_tag"
done

if [[ -f distribution/homebrew/quickstype.rb ]]; then
  pass "Homebrew cask output exists"
else
  fail "Homebrew cask output is missing: distribution/homebrew/quickstype.rb"
fi

if find distribution/winget/manifests -type f -name '*.yaml' >/dev/null 2>&1; then
  if [[ -n "$(find distribution/winget/manifests -type f -name '*.yaml' -print -quit 2>/dev/null)" ]]; then
    pass "Winget manifest YAML exists"
  else
    fail "Winget manifest YAML is missing under distribution/winget/manifests"
  fi
else
  fail "Winget manifest directory is missing: distribution/winget/manifests"
fi

if [[ -s .planning/release-evidence-v1.0.0.md \
  && -f distribution/homebrew/quickstype.rb \
  && -n "$(find distribution/winget/manifests -type f -name '*.yaml' -print -quit 2>/dev/null)" ]]; then
  if bash distribution/validate-from-release-evidence.sh 1.0.0 .planning/release-evidence-v1.0.0.md "$repo" >/tmp/quickstype-distribution-validation.log 2>&1; then
    pass "Distribution manifests match v1.0.0 release evidence"
  else
    fail "Distribution manifests do not match v1.0.0 release evidence"
    sed 's/^/  /' /tmp/quickstype-distribution-validation.log
  fi
fi

if gh repo view "$repo" --json visibility -q .visibility | grep -Fxq PUBLIC; then
  pass "GitHub repository is public"
else
  fail "GitHub repository is not public"
fi

if [[ "$failures" -eq 0 ]]; then
  echo "Release readiness audit passed."
else
  echo "Release readiness audit failed with $failures blocker(s)." >&2
  warn "Do not mark Phase 6, Phase 7, or the persistent goal complete until this audit passes or each failure is explicitly accepted with owner/date/risk."
  exit 1
fi
