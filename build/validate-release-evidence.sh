#!/usr/bin/env bash
set -euo pipefail

evidence="${1:-}"

if [[ -z "$evidence" ]]; then
  echo "Usage: $0 <release-evidence.md>" >&2
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

if grep -Fxq '| Release commit | unknown |' "$evidence"; then
  echo "Release evidence does not identify the release commit: $evidence" >&2
  exit 1
fi

if ! grep -Eq '^\| Release commit \| [a-fA-F0-9]{40} \|$' "$evidence"; then
  echo "Release evidence has invalid release commit format: $evidence" >&2
  exit 1
fi

if ! grep -Eq '^\| Matching release workflow run \| .+ \(completed/success\) \|$' "$evidence"; then
  echo "Release evidence does not identify a successful release workflow run: $evidence" >&2
  exit 1
fi

echo "Release evidence is complete: $evidence"
