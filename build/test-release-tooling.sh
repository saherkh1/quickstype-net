#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

expected="$tmp_dir/expected-secrets.txt"
workflow="$tmp_dir/workflow-secrets.txt"
prereqs="$tmp_dir/prereq-secrets.txt"
template="$tmp_dir/template-secrets.txt"
configure="$tmp_dir/configure-secrets.txt"

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

{
  sed -n '/^scalar_secrets=(/,/^)/p' "$repo_root/build/configure-release-secrets.sh" \
    | grep -E '^[[:space:]]+[A-Z0-9_]+$' \
    | sed 's/^[[:space:]]*//; s/[[:space:]]*$//'
  sed -n '/^p12_secret_specs=(/,/^)/p' "$repo_root/build/configure-release-secrets.sh" \
    | grep -E '^[[:space:]]+"[A-Z0-9_]+:[A-Z0-9_]+"$' \
    | sed 's/^[[:space:]]*"//; s/:.*$//'
} | sort -u > "$configure"

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
compare_secret_set "build/configure-release-secrets.sh" "$configure"

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

require_reference "$repo_root/build/run-v1-release.sh" "validate-manual-matrix.sh"
if grep -Fq "count_table_status" "$repo_root/build/run-v1-release.sh"; then
  echo "build/run-v1-release.sh must use validate-manual-matrix.sh for strict PASS evidence checks." >&2
  exit 1
fi

for evidence in \
  .planning/release-evidence-v0.99.0.md \
  .planning/release-evidence-v0.99.1.md \
  .planning/release-evidence-v1.0.0.md; do
  require_reference "$repo_root/build/audit-release-readiness.sh" "$evidence"
done

require_reference "$repo_root/build/collect-release-evidence.sh" "expected-release-run-id"
require_reference "$repo_root/build/collect-release-evidence.sh" "gh run view"
require_reference "$repo_root/build/run-canary-release.sh" "run-id-output"
require_reference "$repo_root/build/run-canary-release.sh" "dispatch_started_at"
require_reference "$repo_root/build/run-canary-release.sh" "createdAt >="
require_reference "$repo_root/build/run-canary-pair.sh" "run_id_file"
require_reference "$repo_root/build/run-stable-release.sh" "run_id_file"
require_reference "$repo_root/build/audit-release-readiness.sh" "checked scopes, and written rationale"
require_reference "$repo_root/build/run-v1-release.sh" "checked scopes, and written rationale"
require_reference "$repo_root/build/README-release.md" "check every accepted scope"

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
  "$repo_root/build/validate-release-evidence.sh" \
  "$repo_root/build/validate-manual-matrix.sh" \
  "$repo_root/distribution/generate-from-release-evidence.sh" \
  "$repo_root/distribution/homebrew/generate-cask.sh" \
  "$repo_root/distribution/test-generate-from-release-evidence.sh" \
  "$repo_root/distribution/validate-from-release-evidence.sh" \
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

no_rationale_acceptance="$tmp_dir/no-rationale-acceptance.md"
cat > "$no_rationale_acceptance" <<'ACCEPTANCE'
# QuickSType Unverified v1 Release Acceptance

Owner: Saher
Date: 2026-05-16
Risk: High

## Scope

- [x] Missing or deferred HUD injection rows are accepted for this release.
- [x] Missing or deferred canary update rows are accepted for this release.
- [x] Missing or deferred telemetry rows are accepted for this release.
- [x] Missing canary release evidence sign-off is accepted for this release.

## Rationale

Replace this sentence with the release owner rationale and follow-up closure plan.
ACCEPTANCE

if QUICKSTYPE_ACCEPT_UNVERIFIED_V1=1 \
  QUICKSTYPE_UNVERIFIED_V1_ACCEPTANCE_FILE="$no_rationale_acceptance" \
  bash "$repo_root/build/run-v1-release.sh" 1.0.0 owner/repo "$tmp_dir/evidence.md" >"$tmp_dir/no-rationale-acceptance.log" 2>&1; then
  echo "run-v1-release.sh allowed unverified release without a filled rationale." >&2
  exit 1
fi

if ! grep -Fq "Unverified v1 release acceptance is missing Rationale" "$tmp_dir/no-rationale-acceptance.log"; then
  echo "run-v1-release.sh did not explain missing unverified acceptance rationale." >&2
  cat "$tmp_dir/no-rationale-acceptance.log" >&2
  exit 1
fi

missing_scope_acceptance="$tmp_dir/missing-scope-acceptance.md"
cat > "$missing_scope_acceptance" <<'ACCEPTANCE'
# QuickSType Unverified v1 Release Acceptance

Owner: Saher
Date: 2026-05-16
Risk: High

## Scope

- [x] Missing or deferred HUD injection rows are accepted for this release.
- [x] Missing or deferred canary update rows are accepted for this release.
- [x] Missing or deferred telemetry rows are accepted for this release.

## Rationale

Owner accepts this release risk because the remaining evidence will be closed immediately after signed artifacts are available.
ACCEPTANCE

if QUICKSTYPE_ACCEPT_UNVERIFIED_V1=1 \
  QUICKSTYPE_UNVERIFIED_V1_ACCEPTANCE_FILE="$missing_scope_acceptance" \
  bash "$repo_root/build/run-v1-release.sh" 1.0.0 owner/repo "$tmp_dir/evidence.md" >"$tmp_dir/missing-scope-acceptance.log" 2>&1; then
  echo "run-v1-release.sh allowed unverified release without every accepted scope." >&2
  exit 1
fi

if ! grep -Fq "Unverified v1 release acceptance is missing accepted scope" "$tmp_dir/missing-scope-acceptance.log"; then
  echo "run-v1-release.sh did not explain missing unverified acceptance scope." >&2
  cat "$tmp_dir/missing-scope-acceptance.log" >&2
  exit 1
fi

bad_update_matrix="$tmp_dir/bad-update-matrix.md"
cat > "$bad_update_matrix" <<'MATRIX'
| ID | Platform | Scenario | Old version | New version | Install path | Update source | Expected result | Status | Tester/date | Notes |
|----|----------|----------|-------------|-------------|--------------|---------------|-----------------|--------|-------------|-------|
| UPDATE-CANARY-01 | macOS arm64 | Install v0.99.0, update in-app to v0.99.1 | v0.99.0 | v0.99.1 | `/Applications/QuickSType.app` | GitHub Releases canary feed | App updates and relaunches as v0.99.1 | PASS | - | - |
MATRIX

if bash "$repo_root/build/validate-manual-matrix.sh" "$bad_update_matrix" update >"$tmp_dir/bad-update-matrix.log" 2>&1; then
  echo "validate-manual-matrix.sh allowed PASS update evidence without tester/date and notes." >&2
  exit 1
fi

if ! grep -Eq 'Tester/date|Notes evidence' "$tmp_dir/bad-update-matrix.log"; then
  echo "validate-manual-matrix.sh did not explain missing update row evidence." >&2
  cat "$tmp_dir/bad-update-matrix.log" >&2
  exit 1
fi

good_update_matrix="$tmp_dir/good-update-matrix.md"
cat > "$good_update_matrix" <<'MATRIX'
| ID | Platform | Scenario | Old version | New version | Install path | Update source | Expected result | Status | Tester/date | Notes |
|----|----------|----------|-------------|-------------|--------------|---------------|-----------------|--------|-------------|-------|
| UPDATE-CANARY-01 | macOS arm64 | Install v0.99.0, update in-app to v0.99.1 | v0.99.0 | v0.99.1 | `/Applications/QuickSType.app` | GitHub Releases canary feed | App updates and relaunches as v0.99.1 | PASS | Saher 2026-05-16 | macOS 15.5 arm64; QuickSType-canary-Setup.pkg sha256=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef; release URL recorded |
| UPDATE-CANARY-02 | macOS arm64 | Accessibility granted before update | v0.99.0 | v0.99.1 | `/Applications/QuickSType.app` | GitHub Releases canary feed | No Accessibility permission re-prompt; permission remains trusted | PASS | Saher 2026-05-16 | macOS 15.5 arm64; Accessibility stayed trusted after update; release URL recorded |
| UPDATE-CANARY-03 | macOS arm64 | Accessibility missing after update | v0.99.0 | v0.99.1 | `/Applications/QuickSType.app` | GitHub Releases canary feed | General-tab banner appears with Open System Settings action | PASS | Saher 2026-05-16 | macOS 15.5 arm64; revoked Accessibility and banner appeared after update |
| UPDATE-CANARY-04 | Windows x64 | Install v0.99.0, update in-app to v0.99.1 | v0.99.0 | v0.99.1 | Stable Velopack install directory | GitHub Releases canary feed | App updates and relaunches as v0.99.1 | PASS | Saher 2026-05-16 | Windows 11 x64; signed installer sha256=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef |
| UPDATE-CANARY-05 | Windows x64 | Winget-managed marker/config | v0.99.0 | n/a | Winget install path | n/a | UI says updates managed by Winget; no in-app update prompt | PASS | Saher 2026-05-16 | Windows 11 x64; managed_package_manager=winget disables app update action |
| UPDATE-CANARY-06 | macOS arm64 | Homebrew-managed marker/config | v0.99.0 | n/a | Homebrew cask app path | n/a | UI says updates managed by Homebrew; no in-app update prompt | PASS | Saher 2026-05-16 | macOS 15.5 arm64; managed_package_manager=homebrew disables app update action |
MATRIX

bash "$repo_root/build/validate-manual-matrix.sh" "$good_update_matrix" update >/dev/null

truncated_update_matrix="$tmp_dir/truncated-update-matrix.md"
head -n 3 "$good_update_matrix" >"$truncated_update_matrix"

if bash "$repo_root/build/validate-manual-matrix.sh" "$truncated_update_matrix" update >"$tmp_dir/truncated-update-matrix.log" 2>&1; then
  echo "validate-manual-matrix.sh allowed an incomplete update matrix." >&2
  exit 1
fi

if ! grep -Fq 'expected 6 update matrix rows' "$tmp_dir/truncated-update-matrix.log"; then
  echo "validate-manual-matrix.sh did not explain incomplete update matrix row count." >&2
  cat "$tmp_dir/truncated-update-matrix.log" >&2
  exit 1
fi

bad_evidence="$tmp_dir/bad-release-evidence-v1.0.0.md"
cat > "$bad_evidence" <<'EVIDENCE'
# QuickSType Release Evidence - v1.0.0

| Field | Value |
|-------|-------|
| Release commit | unknown |
| Matching release workflow run | not found for release commit |

## Manual Sign-Off

- [x] macOS package signing identity matches expected Developer ID Application/Installer identities.
- [x] macOS notarization and stapler validation passed in the release workflow.
- [x] Windows Azure Artifact Signing completed for publish directory and installer.
- [x] `tests/manual/UPDATE_CANARY_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] `tests/manual/INJECTION_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] `tests/manual/TELEMETRY_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] Homebrew and Winget manifest generators use the exact URLs and SHA-256 values above.
EVIDENCE

if bash "$repo_root/build/validate-release-evidence.sh" "$bad_evidence" >"$tmp_dir/bad-evidence-validation.log" 2>&1; then
  echo "validate-release-evidence.sh allowed release evidence without a release commit/workflow." >&2
  exit 1
fi

if ! grep -Eq 'release commit|successful release workflow run' "$tmp_dir/bad-evidence-validation.log"; then
  echo "validate-release-evidence.sh did not explain invalid release evidence." >&2
  cat "$tmp_dir/bad-evidence-validation.log" >&2
  exit 1
fi

missing_assets_evidence="$tmp_dir/missing-assets-release-evidence-v1.0.0.md"
cat > "$missing_assets_evidence" <<'EVIDENCE'
# QuickSType Release Evidence - v1.0.0

| Field | Value |
|-------|-------|
| Tag | v1.0.0 |
| Release commit | 0123456789abcdef0123456789abcdef01234567 |
| Prerelease | false |
| Matching release workflow run | https://github.com/saherkh1/quickstype-net/actions/runs/123 (completed/success) |

## Manual Sign-Off

- [x] macOS package signing identity matches expected Developer ID Application/Installer identities.
- [x] macOS notarization and stapler validation passed in the release workflow.
- [x] Windows Azure Artifact Signing completed for publish directory and installer.
- [x] `tests/manual/UPDATE_CANARY_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] `tests/manual/INJECTION_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] `tests/manual/TELEMETRY_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] Homebrew and Winget manifest generators use the exact URLs and SHA-256 values above.
EVIDENCE

if bash "$repo_root/build/validate-release-evidence.sh" "$missing_assets_evidence" >"$tmp_dir/missing-assets-validation.log" 2>&1; then
  echo "validate-release-evidence.sh allowed release evidence without setup asset checksums." >&2
  exit 1
fi

if ! grep -Fq 'setup assets with SHA-256 values' "$tmp_dir/missing-assets-validation.log"; then
  echo "validate-release-evidence.sh did not explain missing setup asset checksums." >&2
  cat "$tmp_dir/missing-assets-validation.log" >&2
  exit 1
fi

missing_signoff_evidence="$tmp_dir/missing-signoff-release-evidence-v1.0.0.md"
cat > "$missing_signoff_evidence" <<'EVIDENCE'
# QuickSType Release Evidence - v1.0.0

| Field | Value |
|-------|-------|
| Tag | v1.0.0 |
| Release commit | 0123456789abcdef0123456789abcdef01234567 |
| Prerelease | false |
| Matching release workflow run | https://github.com/saherkh1/quickstype-net/actions/runs/123 (completed/success) |

## Asset Checksums

| Asset | Size bytes | SHA-256 |
|-------|------------|---------|
| QuickSType-stable-Setup.pkg | 123 | 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef |
| QuickSType-stable-Setup.exe | 456 | fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210 |

## Manual Sign-Off

- [x] macOS package signing identity matches expected Developer ID Application/Installer identities.
- [x] macOS notarization and stapler validation passed in the release workflow.
- [x] Windows Azure Artifact Signing completed for publish directory and installer.
- [x] `tests/manual/UPDATE_CANARY_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] `tests/manual/INJECTION_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] Homebrew and Winget manifest generators use the exact URLs and SHA-256 values above.
EVIDENCE

if bash "$repo_root/build/validate-release-evidence.sh" "$missing_signoff_evidence" >"$tmp_dir/missing-signoff-validation.log" 2>&1; then
  echo "validate-release-evidence.sh allowed release evidence with a missing manual sign-off line." >&2
  exit 1
fi

if ! grep -Fq 'missing required manual sign-off' "$tmp_dir/missing-signoff-validation.log"; then
  echo "validate-release-evidence.sh did not explain the missing manual sign-off line." >&2
  cat "$tmp_dir/missing-signoff-validation.log" >&2
  exit 1
fi

good_evidence="$tmp_dir/good-release-evidence-v1.0.0.md"
cat > "$good_evidence" <<'EVIDENCE'
# QuickSType Release Evidence - v1.0.0

| Field | Value |
|-------|-------|
| Tag | v1.0.0 |
| Release commit | 0123456789abcdef0123456789abcdef01234567 |
| Prerelease | false |
| Matching release workflow run | https://github.com/saherkh1/quickstype-net/actions/runs/123 (completed/success) |

## Asset Checksums

| Asset | Size bytes | SHA-256 |
|-------|------------|---------|
| QuickSType-stable-Setup.pkg | 123 | 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef |
| QuickSType-stable-Setup.exe | 456 | fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210 |

## Manual Sign-Off

- [x] macOS package signing identity matches expected Developer ID Application/Installer identities.
- [x] macOS notarization and stapler validation passed in the release workflow.
- [x] Windows Azure Artifact Signing completed for publish directory and installer.
- [x] `tests/manual/UPDATE_CANARY_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] `tests/manual/INJECTION_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] `tests/manual/TELEMETRY_MATRIX.md` rows updated with PASS status plus tester/date/notes evidence.
- [x] Homebrew and Winget manifest generators use the exact URLs and SHA-256 values above.
EVIDENCE

bash "$repo_root/build/validate-release-evidence.sh" "$good_evidence" false v1.0.0 >/dev/null

run_id_evidence="$tmp_dir/run-id-release-evidence-v1.0.0.md"
sed 's#(completed/success)#(completed/success; run 123456789)#' "$good_evidence" >"$run_id_evidence"
bash "$repo_root/build/validate-release-evidence.sh" "$run_id_evidence" false v1.0.0 >/dev/null

if bash "$repo_root/build/validate-release-evidence.sh" "$good_evidence" true >"$tmp_dir/prerelease-validation.log" 2>&1; then
  echo "validate-release-evidence.sh allowed stable evidence when canary prerelease evidence was required." >&2
  exit 1
fi

if ! grep -Fq 'expected prerelease=true' "$tmp_dir/prerelease-validation.log"; then
  echo "validate-release-evidence.sh did not explain prerelease mismatch." >&2
  cat "$tmp_dir/prerelease-validation.log" >&2
  exit 1
fi

if bash "$repo_root/build/validate-release-evidence.sh" "$good_evidence" false v0.99.1 >"$tmp_dir/tag-validation.log" 2>&1; then
  echo "validate-release-evidence.sh allowed evidence for the wrong release tag." >&2
  exit 1
fi

if ! grep -Fq 'expected tag=v0.99.1' "$tmp_dir/tag-validation.log"; then
  echo "validate-release-evidence.sh did not explain tag mismatch." >&2
  cat "$tmp_dir/tag-validation.log" >&2
  exit 1
fi

echo "Release tooling consistency test passed."
