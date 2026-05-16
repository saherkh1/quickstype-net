#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

expected="$tmp_dir/expected-secrets.txt"
workflow="$tmp_dir/workflow-secrets.txt"
prereqs="$tmp_dir/prereq-secrets.txt"
template="$tmp_dir/template-secrets.txt"

cat > "$expected" <<'SECRETS'
APPLE_APP_SPECIFIC_PASSWORD
APPLE_DEVELOPER_ID_APPLICATION_IDENTITY
APPLE_DEVELOPER_ID_APPLICATION_P12_BASE64
APPLE_DEVELOPER_ID_APPLICATION_PASSWORD
APPLE_DEVELOPER_ID_INSTALLER_IDENTITY
APPLE_DEVELOPER_ID_INSTALLER_P12_BASE64
APPLE_DEVELOPER_ID_INSTALLER_PASSWORD
APPLE_ID
APPLE_KEYCHAIN_PASSWORD
APPLE_TEAM_ID
AZURE_ARTIFACT_SIGNING_ACCOUNT
AZURE_ARTIFACT_SIGNING_CERT_PROFILE
AZURE_ARTIFACT_SIGNING_ENDPOINT
AZURE_CLIENT_ID
AZURE_SUBSCRIPTION_ID
AZURE_TENANT_ID
SECRETS

sort -o "$expected" "$expected"

grep -Eoh 'secrets\.[A-Z0-9_]+' "$repo_root/.github/workflows/release.yml" \
  | sed 's/^secrets\.//' \
  | sort -u > "$workflow"

sed -n '/^required_secrets=(/,/^)/p' "$repo_root/build/check-release-prereqs.sh" \
  | grep -E '^[[:space:]]+[A-Z0-9_]+$' \
  | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' \
  | sort -u > "$prereqs"

grep -E '^[A-Z0-9_]+=' "$repo_root/build/release-secrets.env.example" \
  | cut -d= -f1 \
  | grep -Ev '_P12_PATH$' \
  | sort -u > "$template"

compare_secret_set() {
  local label="$1"
  local actual="$2"

  if ! diff -u "$expected" "$actual"; then
    echo "$label release secret set drifted from the expected contract." >&2
    exit 1
  fi
}

compare_secret_set ".github/workflows/release.yml" "$workflow"
compare_secret_set "build/check-release-prereqs.sh" "$prereqs"
compare_secret_set "build/release-secrets.env.example" "$template"

require_reference() {
  local file="$1"
  local expected="$2"

  if ! grep -Fq "$expected" "$file"; then
    echo "${file#"$repo_root"/} is missing required release-gate reference: $expected" >&2
    exit 1
  fi
}

for matrix in \
  tests/manual/UPDATE_CANARY_MATRIX.md \
  tests/manual/INJECTION_MATRIX.md \
  tests/manual/TELEMETRY_MATRIX.md; do
  require_reference "$repo_root/build/audit-release-readiness.sh" "$matrix"
  require_reference "$repo_root/build/run-v1-release.sh" "$matrix"
  require_reference "$repo_root/build/collect-release-evidence.sh" "$matrix"
  test -s "$repo_root/$matrix"
done

for evidence in \
  .planning/release-evidence-v0.99.0.md \
  .planning/release-evidence-v0.99.1.md \
  .planning/release-evidence-v1.0.0.md; do
  require_reference "$repo_root/build/audit-release-readiness.sh" "$evidence"
done

if command -v shellcheck >/dev/null 2>&1; then
  shellcheck \
    "$repo_root"/build/*.sh \
    "$repo_root"/distribution/*.sh \
    "$repo_root"/distribution/homebrew/*.sh \
    "$repo_root"/distribution/winget/*.sh
else
  echo "shellcheck is required for release tooling validation." >&2
  exit 1
fi

bash -n \
  "$repo_root/build/audit-release-readiness.sh" \
  "$repo_root/build/check-release-prereqs.sh" \
  "$repo_root/build/collect-release-evidence.sh" \
  "$repo_root/build/configure-release-secrets.sh" \
  "$repo_root/build/run-canary-pair.sh" \
  "$repo_root/build/run-canary-release.sh" \
  "$repo_root/build/run-stable-release.sh" \
  "$repo_root/build/run-v1-release.sh" \
  "$repo_root/distribution/generate-from-release-evidence.sh" \
  "$repo_root/distribution/homebrew/generate-cask.sh" \
  "$repo_root/distribution/test-generate-from-release-evidence.sh" \
  "$repo_root/distribution/winget/generate-winget-manifests.sh"

if QUICKSTYPE_ACCEPT_UNVERIFIED_V1=1 \
  QUICKSTYPE_UNVERIFIED_V1_ACCEPTANCE_FILE="$tmp_dir/missing-acceptance.md" \
  bash "$repo_root/build/run-v1-release.sh" 1.0.0 owner/repo "$tmp_dir/evidence.md" >"$tmp_dir/unverified-v1.log" 2>&1; then
  echo "run-v1-release.sh allowed unverified release without acceptance file." >&2
  exit 1
fi

if ! grep -Fq "Unverified v1 release acceptance is missing" "$tmp_dir/unverified-v1.log"; then
  echo "run-v1-release.sh did not explain missing unverified acceptance." >&2
  cat "$tmp_dir/unverified-v1.log" >&2
  exit 1
fi

echo "Release tooling consistency test passed."
