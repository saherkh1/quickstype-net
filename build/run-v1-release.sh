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

require_clean_manual_matrix() {
  local file="$1"
  local label="$2"
  local row_pattern="$3"

  if [[ ! -f "$file" ]]; then
    echo "$label is missing: $file" >&2
    return 1
  fi

  local pending deferred failed
  pending="$(count_table_status "$file" PENDING "$row_pattern")"
  deferred="$(count_table_status "$file" DEFERRED "$row_pattern")"
  failed="$(count_table_status "$file" FAIL "$row_pattern")"

  if [[ "$pending" -ne 0 || "$deferred" -ne 0 || "$failed" -ne 0 ]]; then
    echo "$label is not accepted (PENDING=$pending, DEFERRED=$deferred, FAIL=$failed): $file" >&2
    return 1
  fi
}

require_release_evidence_signoff() {
  local file="$1"

  if ! bash "$repo_root/build/validate-release-evidence.sh" "$file" true >/tmp/quickstype-v1-canary-evidence.log 2>&1; then
    sed "s#${repo_root}/##g" /tmp/quickstype-v1-canary-evidence.log >&2
    return 1
  fi
}

require_unverified_acceptance() {
  local acceptance_file="${QUICKSTYPE_UNVERIFIED_V1_ACCEPTANCE_FILE:-$repo_root/.planning/unverified-v1-release-acceptance.md}"

  if [[ ! -s "$acceptance_file" ]]; then
    echo "Unverified v1 release acceptance is missing: ${acceptance_file#"$repo_root"/}" >&2
    echo "Copy build/unverified-v1-release-acceptance.template.md to that path, fill owner/date/risk, and check accepted scopes." >&2
    return 1
  fi

  if ! grep -Eq '^Owner:[[:space:]]*[^[:space:]]' "$acceptance_file"; then
    echo "Unverified v1 release acceptance is missing Owner: ${acceptance_file#"$repo_root"/}" >&2
    return 1
  fi

  if ! grep -Eq '^Date:[[:space:]]*[0-9]{4}-[0-9]{2}-[0-9]{2}' "$acceptance_file"; then
    echo "Unverified v1 release acceptance is missing ISO Date: ${acceptance_file#"$repo_root"/}" >&2
    return 1
  fi

  if ! grep -Eq '^Risk:[[:space:]]*[^[:space:]]' "$acceptance_file"; then
    echo "Unverified v1 release acceptance is missing Risk: ${acceptance_file#"$repo_root"/}" >&2
    return 1
  fi

  if grep -Eq '^- \[ \]' "$acceptance_file"; then
    echo "Unverified v1 release acceptance has unchecked scope boxes: ${acceptance_file#"$repo_root"/}" >&2
    return 1
  fi

  echo "Using unverified v1 release acceptance: ${acceptance_file#"$repo_root"/}"
}

verify_v1_release_gate() {
  local failures=0

  for required_evidence in \
    "$repo_root/.planning/release-evidence-v0.99.0.md" \
    "$repo_root/.planning/release-evidence-v0.99.1.md"; do
    require_release_evidence_signoff "$required_evidence" || failures=$((failures + 1))
  done

  require_clean_manual_matrix "$repo_root/tests/manual/UPDATE_CANARY_MATRIX.md" "Canary update matrix" '^UPDATE-CANARY-[0-9]+$' || failures=$((failures + 1))
  require_clean_manual_matrix "$repo_root/tests/manual/INJECTION_MATRIX.md" "HUD injection matrix" '^[0-9]+$' || failures=$((failures + 1))
  require_clean_manual_matrix "$repo_root/tests/manual/TELEMETRY_MATRIX.md" "Telemetry matrix" '^TELEMETRY-[0-9]+$' || failures=$((failures + 1))

  if [[ "$failures" -ne 0 ]]; then
    echo "Stable v$version release gate failed." >&2
    echo "Complete Phase 5/6 evidence first, or set QUICKSTYPE_ACCEPT_UNVERIFIED_V1=1 only after owner/date/risk acceptance is recorded." >&2
    return 1
  fi
}

if [[ "${QUICKSTYPE_ACCEPT_UNVERIFIED_V1:-}" == "1" ]]; then
  require_unverified_acceptance
  echo "WARNING: bypassing v1 manual evidence gate because QUICKSTYPE_ACCEPT_UNVERIFIED_V1=1"
else
  verify_v1_release_gate
fi

echo "Running stable v$version release for $repo"
bash "$script_dir/run-stable-release.sh" "$version" "$repo" "$evidence_output"

echo "Generating distribution manifests from $evidence_output"
bash "$repo_root/distribution/generate-from-release-evidence.sh" "$version" "$evidence_output" "$repo"

echo "Validating distribution manifests against $evidence_output"
bash "$repo_root/distribution/validate-from-release-evidence.sh" "$version" "$evidence_output" "$repo"

echo "Validating stable release evidence"
bash "$repo_root/build/validate-release-evidence.sh" "$evidence_output" false

echo "v$version release evidence and distribution manifests are ready."
echo "Next: validate Homebrew/Winget installs and rerun build/audit-release-readiness.sh $repo"
