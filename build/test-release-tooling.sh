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

echo "Release tooling consistency test passed."
