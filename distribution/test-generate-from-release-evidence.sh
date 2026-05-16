#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

sha_mac="0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
sha_win="fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210"
sha_win_upper="$(printf '%s' "$sha_win" | tr '[:lower:]' '[:upper:]')"
evidence="$tmp_dir/release-evidence-v1.0.0.md"

cat > "$evidence" <<EVIDENCE
# QuickSType Release Evidence - v1.0.0

| Field | Value |
|-------|-------|
| Tag | v1.0.0 |
| Release commit | 0123456789abcdef0123456789abcdef01234567 |
| Prerelease | false |
| Matching release workflow run | https://github.com/saherkh1/quickstype-net/actions/runs/123 (completed/success) |

## Asset Checksums

| Asset | Size | SHA-256 |
|-------|------|---------|
| QuickSType-stable-Setup.pkg | 123 | $sha_mac |
| QuickSType-stable-Setup.exe | 456 | $sha_win |

## Manual Sign-Off

- [x] macOS package signing identity matches expected Developer ID Application/Installer identities.
- [x] macOS notarization and stapler validation passed in the release workflow.
- [x] Windows Azure Artifact Signing completed for publish directory and installer.
- [x] \`tests/manual/UPDATE_CANARY_MATRIX.md\` rows updated with PASS status plus tester/date/notes evidence.
- [x] \`tests/manual/INJECTION_MATRIX.md\` rows updated with PASS status plus tester/date/notes evidence.
- [x] \`tests/manual/TELEMETRY_MATRIX.md\` rows updated with PASS status plus tester/date/notes evidence.
- [x] Homebrew and Winget manifest generators use the exact URLs and SHA-256 values above.
EVIDENCE

incomplete_evidence="$tmp_dir/incomplete-release-evidence-v1.0.0.md"
cat > "$incomplete_evidence" <<EVIDENCE
# Release Evidence v1.0.0

| Asset | Size | SHA-256 |
|-------|------|---------|
| QuickSType-stable-Setup.pkg | 123 | $sha_mac |
| QuickSType-stable-Setup.exe | 456 | $sha_win |
EVIDENCE

if (
  cd "$tmp_dir"
  bash "$repo_root/distribution/generate-from-release-evidence.sh" \
    1.0.0 \
    "$incomplete_evidence" \
    saherkh1/quickstype-net >"$tmp_dir/incomplete-generate.log" 2>&1
); then
  echo "Expected incomplete release evidence generation to fail." >&2
  exit 1
fi

assert_contains() {
  local file="$1"
  local text="$2"

  if ! grep -Fq "$text" "$file"; then
    echo "Expected $file to contain: $text" >&2
    echo "--- $file ---" >&2
    sed -n '1,220p' "$file" >&2
    exit 1
  fi
}

expect_generator_failure() {
  local label="$1"
  local expected="$2"
  shift 2

  if "$@" >"$tmp_dir/$label.log" 2>&1; then
    echo "Expected $label to fail." >&2
    exit 1
  fi

  assert_contains "$tmp_dir/$label.log" "$expected"
}

assert_contains "$tmp_dir/incomplete-generate.log" 'missing required manual sign-off'

expect_generator_failure \
  bad-homebrew-host \
  "Homebrew package URL must be a GitHub release QuickSType .pkg asset URL." \
  bash "$repo_root/distribution/homebrew/generate-cask.sh" \
    1.0.0 \
    "https://example.com/saherkh1/quickstype-net/releases/download/v1.0.0/QuickSType-stable-Setup.pkg" \
    "$sha_mac" \
    "$tmp_dir/bad-homebrew-host.rb"

expect_generator_failure \
  bad-homebrew-tag \
  "Homebrew package URL release tag must match version v1.0.0." \
  bash "$repo_root/distribution/homebrew/generate-cask.sh" \
    1.0.0 \
    "https://github.com/saherkh1/quickstype-net/releases/download/v1.0.1/QuickSType-stable-Setup.pkg" \
    "$sha_mac" \
    "$tmp_dir/bad-homebrew-tag.rb"

expect_generator_failure \
  bad-homebrew-extension \
  "Homebrew package URL must be a GitHub release QuickSType .pkg asset URL." \
  bash "$repo_root/distribution/homebrew/generate-cask.sh" \
    1.0.0 \
    "https://github.com/saherkh1/quickstype-net/releases/download/v1.0.0/QuickSType-stable-Setup.exe" \
    "$sha_mac" \
    "$tmp_dir/bad-homebrew-extension.rb"

expect_generator_failure \
  bad-winget-host \
  "Winget installer URL must be a GitHub release QuickSType .exe asset URL." \
  bash "$repo_root/distribution/winget/generate-winget-manifests.sh" \
    1.0.0 \
    "https://example.com/saherkh1/quickstype-net/releases/download/v1.0.0/QuickSType-stable-Setup.exe" \
    "$sha_win" \
    "$tmp_dir/bad-winget-host"

expect_generator_failure \
  bad-winget-tag \
  "Winget installer URL release tag must match version v1.0.0." \
  bash "$repo_root/distribution/winget/generate-winget-manifests.sh" \
    1.0.0 \
    "https://github.com/saherkh1/quickstype-net/releases/download/v1.0.1/QuickSType-stable-Setup.exe" \
    "$sha_win" \
    "$tmp_dir/bad-winget-tag"

expect_generator_failure \
  bad-winget-extension \
  "Winget installer URL must be a GitHub release QuickSType .exe asset URL." \
  bash "$repo_root/distribution/winget/generate-winget-manifests.sh" \
    1.0.0 \
    "https://github.com/saherkh1/quickstype-net/releases/download/v1.0.0/QuickSType-stable-Setup.pkg" \
    "$sha_win" \
    "$tmp_dir/bad-winget-extension"

(
  cd "$tmp_dir"
  bash "$repo_root/distribution/generate-from-release-evidence.sh" \
    1.0.0 \
    "$evidence" \
    saherkh1/quickstype-net
)

cask="$tmp_dir/distribution/homebrew/quickstype.rb"
winget_root="$tmp_dir/distribution/winget/manifests/q/QuickSType/QuickSType/1.0.0"
winget_version="$winget_root/QuickSType.QuickSType.yaml"
winget_locale="$winget_root/QuickSType.QuickSType.locale.en-US.yaml"
winget_installer="$winget_root/QuickSType.QuickSType.installer.yaml"

for file in "$cask" "$winget_version" "$winget_locale" "$winget_installer"; do
  if [[ ! -f "$file" ]]; then
    echo "Expected generated file missing: $file" >&2
    exit 1
  fi
done

assert_contains "$cask" 'cask "quickstype" do'
assert_contains "$cask" 'version "1.0.0"'
assert_contains "$cask" "sha256 \"$sha_mac\""
assert_contains "$cask" 'url "https://github.com/saherkh1/quickstype-net/releases/download/v1.0.0/QuickSType-stable-Setup.pkg"'
assert_contains "$cask" 'pkg "QuickSType-stable-Setup.pkg"'

assert_contains "$winget_version" 'PackageIdentifier: QuickSType.QuickSType'
assert_contains "$winget_version" 'PackageVersion: 1.0.0'
assert_contains "$winget_locale" 'PackageName: QuickSType'
assert_contains "$winget_installer" 'InstallerType: exe'
assert_contains "$winget_installer" 'InstallerUrl: https://github.com/saherkh1/quickstype-net/releases/download/v1.0.0/QuickSType-stable-Setup.exe'
assert_contains "$winget_installer" "InstallerSha256: $sha_win_upper"

(
  cd "$tmp_dir"
  bash "$repo_root/distribution/validate-from-release-evidence.sh" \
    1.0.0 \
    "$evidence" \
    saherkh1/quickstype-net
)

perl -0pi -e 's/QuickSType-stable-Setup[.]pkg/QuickSType-stale-Setup.pkg/g' "$cask"
if (
  cd "$tmp_dir"
  bash "$repo_root/distribution/validate-from-release-evidence.sh" \
    1.0.0 \
    "$evidence" \
    saherkh1/quickstype-net >"$tmp_dir/stale-validation.log" 2>&1
); then
  echo "Expected stale Homebrew cask validation to fail." >&2
  exit 1
fi

assert_contains "$tmp_dir/stale-validation.log" 'does not match release evidence'

echo "Distribution manifest generator fixture test passed."
