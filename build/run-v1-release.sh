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

  if [[ ! -s "$file" ]]; then
    echo "Required canary evidence is missing: ${file#"$repo_root"/}" >&2
    return 1
  fi

  if grep -Eq '^- \[ \]' "$file"; then
    echo "Required canary evidence has incomplete manual sign-off: ${file#"$repo_root"/}" >&2
    return 1
  fi
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
  echo "WARNING: bypassing v1 manual evidence gate because QUICKSTYPE_ACCEPT_UNVERIFIED_V1=1"
else
  verify_v1_release_gate
fi

echo "Running stable v$version release for $repo"
bash "$script_dir/run-stable-release.sh" "$version" "$repo" "$evidence_output"

echo "Generating distribution manifests from $evidence_output"
bash "$repo_root/distribution/generate-from-release-evidence.sh" "$version" "$evidence_output" "$repo"

echo "v$version release evidence and distribution manifests are ready."
echo "Next: validate Homebrew/Winget installs and rerun build/audit-release-readiness.sh $repo"
