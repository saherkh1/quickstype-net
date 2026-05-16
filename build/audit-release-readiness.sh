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

count_table_status() {
  local file="$1"
  local status="$2"
  local row_pattern="$3"
  awk -F'|' -v status="$status" -v row_pattern="$row_pattern" '
    /^\|/ {
      first = $2
      gsub(/^[ \t]+|[ \t]+$/, "", first)
      if (first !~ row_pattern) {
        next
      }
      for (i = 1; i <= NF; i++) {
        gsub(/^[ \t]+|[ \t]+$/, "", $i)
        if ($i == status) {
          count++
        }
      }
    }
    END { print count + 0 }
  ' "$file"
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

  if [[ ! -s "$evidence" ]]; then
    fail "Release evidence is missing: $evidence"
    return
  fi

  if grep -Eq '^- \[ \]' "$evidence"; then
    fail "Release evidence has incomplete manual sign-off: $evidence"
    return
  fi

  pass "Release evidence exists with completed manual sign-off: $evidence"
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
if [[ -f "$injection_matrix" ]]; then
  injection_pending="$(count_table_status "$injection_matrix" PENDING '^[0-9]+$')"
  injection_deferred="$(count_table_status "$injection_matrix" DEFERRED '^[0-9]+$')"
  injection_fail="$(count_table_status "$injection_matrix" FAIL '^[0-9]+$')"
  if [[ "$injection_pending" -eq 0 && "$injection_deferred" -eq 0 && "$injection_fail" -eq 0 ]]; then
    pass "HUD injection matrix has no PENDING/DEFERRED/FAIL rows"
  else
    fail "HUD injection matrix is not fully accepted (PENDING=$injection_pending, DEFERRED=$injection_deferred, FAIL=$injection_fail)"
  fi
else
  fail "HUD injection matrix is missing: $injection_matrix"
fi

update_matrix="tests/manual/UPDATE_CANARY_MATRIX.md"
if [[ -f "$update_matrix" ]]; then
  update_pending="$(count_table_status "$update_matrix" PENDING '^UPDATE-CANARY-[0-9]+$')"
  update_deferred="$(count_table_status "$update_matrix" DEFERRED '^UPDATE-CANARY-[0-9]+$')"
  update_fail="$(count_table_status "$update_matrix" FAIL '^UPDATE-CANARY-[0-9]+$')"
  if [[ "$update_pending" -eq 0 && "$update_deferred" -eq 0 && "$update_fail" -eq 0 ]]; then
    pass "Canary update matrix has no PENDING/DEFERRED/FAIL rows"
  else
    fail "Canary update matrix is not fully accepted (PENDING=$update_pending, DEFERRED=$update_deferred, FAIL=$update_fail)"
  fi
else
  fail "Canary update matrix is missing: $update_matrix"
fi

telemetry_matrix="tests/manual/TELEMETRY_MATRIX.md"
if [[ -f "$telemetry_matrix" ]]; then
  telemetry_pending="$(count_table_status "$telemetry_matrix" PENDING '^TELEMETRY-[0-9]+$')"
  telemetry_deferred="$(count_table_status "$telemetry_matrix" DEFERRED '^TELEMETRY-[0-9]+$')"
  telemetry_fail="$(count_table_status "$telemetry_matrix" FAIL '^TELEMETRY-[0-9]+$')"
  if [[ "$telemetry_pending" -eq 0 && "$telemetry_deferred" -eq 0 && "$telemetry_fail" -eq 0 ]]; then
    pass "Telemetry matrix has no PENDING/DEFERRED/FAIL rows"
  else
    fail "Telemetry matrix is not fully accepted (PENDING=$telemetry_pending, DEFERRED=$telemetry_deferred, FAIL=$telemetry_fail)"
  fi
else
  fail "Telemetry matrix is missing: $telemetry_matrix"
fi

for evidence in \
  .planning/release-evidence-v0.99.0.md \
  .planning/release-evidence-v0.99.1.md \
  .planning/release-evidence-v1.0.0.md; do
  check_release_evidence "$evidence"
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
