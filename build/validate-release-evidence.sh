#!/usr/bin/env bash
set -euo pipefail

evidence="${1:-}"
expected_prerelease="${2:-}"
expected_tag="${3:-}"

if [[ -z "$evidence" ]]; then
  echo "Usage: $0 <release-evidence.md> [expected-prerelease:true|false] [expected-tag]" >&2
  exit 64
fi

if [[ -n "$expected_prerelease" && "$expected_prerelease" != "true" && "$expected_prerelease" != "false" ]]; then
  echo "Expected prerelease value must be true or false: $expected_prerelease" >&2
  exit 64
fi

if [[ -n "$expected_tag" && ! "$expected_tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$ ]]; then
  echo "Expected tag must be a v-prefixed SemVer tag: $expected_tag" >&2
  exit 64
fi

if [[ ! -s "$evidence" ]]; then
  echo "Release evidence is missing: $evidence" >&2
  exit 1
fi

if grep -Eq '^- \[ \]' "$evidence"; then
  echo "Release evidence has incomplete manual sign-off: $evidence" >&2
  exit 1
fi

required_signoffs=(
  '- [x] macOS package signing identity matches expected Developer ID Application/Installer identities.'
  '- [x] macOS notarization and stapler validation passed in the release workflow.'
  '- [x] Windows Azure Artifact Signing completed for publish directory and installer.'
  "- [x] \`tests/manual/UPDATE_CANARY_MATRIX.md\` rows updated with PASS status plus tester/date/notes evidence."
  "- [x] \`tests/manual/INJECTION_MATRIX.md\` rows updated with PASS status plus tester/date/notes evidence."
  "- [x] \`tests/manual/TELEMETRY_MATRIX.md\` rows updated with PASS status plus tester/date/notes evidence."
  '- [x] Homebrew and Winget manifest generators use the exact URLs and SHA-256 values above.'
)

for required_signoff in "${required_signoffs[@]}"; do
  if ! grep -Fxq -- "$required_signoff" "$evidence"; then
    echo "Release evidence is missing required manual sign-off: $required_signoff" >&2
    exit 1
  fi
done

if grep -Fxq '| Release commit | unknown |' "$evidence"; then
  echo "Release evidence does not identify the release commit: $evidence" >&2
  exit 1
fi

if ! grep -Eq '^\| Release commit \| [a-fA-F0-9]{40} \|$' "$evidence"; then
  echo "Release evidence has invalid release commit format: $evidence" >&2
  exit 1
fi

if ! grep -Eq '^\| Matching release workflow run \| .+ \(completed/success(; run [0-9]+)?\) \|$' "$evidence"; then
  echo "Release evidence does not identify a successful release workflow run: $evidence" >&2
  exit 1
fi

if [[ -n "$expected_prerelease" ]] && ! grep -Fxq "| Prerelease | $expected_prerelease |" "$evidence"; then
  echo "Release evidence does not match expected prerelease=$expected_prerelease: $evidence" >&2
  exit 1
fi

if [[ -n "$expected_tag" ]] && ! grep -Fxq "| Tag | $expected_tag |" "$evidence"; then
  echo "Release evidence does not match expected tag=$expected_tag: $evidence" >&2
  exit 1
fi

if ! awk -F'|' '
  /^\|/ {
    asset = $2
    sha = $4
    gsub(/^[ \t]+|[ \t]+$/, "", asset)
    gsub(/^[ \t]+|[ \t]+$/, "", sha)
    if (asset ~ /^QuickSType-.*-Setup[.]pkg$/ && sha ~ /^[a-fA-F0-9]{64}$/) {
      found_pkg = 1
    }
    if (asset ~ /^QuickSType-.*-Setup[.]exe$/ && sha ~ /^[a-fA-F0-9]{64}$/) {
      found_exe = 1
    }
  }
  END { exit !(found_pkg && found_exe) }
' "$evidence"; then
  echo "Release evidence must include QuickSType .pkg and .exe setup assets with SHA-256 values: $evidence" >&2
  exit 1
fi

echo "Release evidence is complete: $evidence"
