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

scalar_secrets=(
  APPLE_DEVELOPER_ID_APPLICATION_PASSWORD
  APPLE_DEVELOPER_ID_INSTALLER_PASSWORD
  APPLE_KEYCHAIN_PASSWORD
  APPLE_DEVELOPER_ID_APPLICATION_IDENTITY
  APPLE_DEVELOPER_ID_INSTALLER_IDENTITY
  APPLE_ID
  APPLE_TEAM_ID
  APPLE_APP_SPECIFIC_PASSWORD
  AZURE_CLIENT_ID
  AZURE_TENANT_ID
  AZURE_SUBSCRIPTION_ID
  AZURE_ARTIFACT_SIGNING_ENDPOINT
  AZURE_ARTIFACT_SIGNING_ACCOUNT
  AZURE_ARTIFACT_SIGNING_CERT_PROFILE
)

p12_secret_specs=(
  "APPLE_DEVELOPER_ID_APPLICATION_P12_BASE64:APPLE_DEVELOPER_ID_APPLICATION_P12_PATH"
  "APPLE_DEVELOPER_ID_INSTALLER_P12_BASE64:APPLE_DEVELOPER_ID_INSTALLER_P12_PATH"
)

missing=()

require_command() {
  local command_name="$1"
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command not found: $command_name" >&2
    exit 1
  fi
}

get_env_value() {
  local name="$1"
  printf '%s' "${!name:-}"
}

encode_base64_file() {
  local path="$1"
  if [[ ! -f "$path" ]]; then
    echo "P12 file does not exist: $path" >&2
    exit 1
  fi

  base64 <"$path" | tr -d '\n'
}

set_secret() {
  local name="$1"
  local value="$2"

  printf '%s' "$value" | gh secret set "$name" --repo "$repo"
  echo "secret: $name configured"
}

require_command gh
require_command git
require_command base64
require_command tr

if ! gh auth status >/dev/null 2>&1; then
  echo "GitHub CLI is not authenticated. Run: gh auth login" >&2
  exit 1
fi

for spec in "${p12_secret_specs[@]}"; do
  secret_name="${spec%%:*}"
  path_name="${spec##*:}"
  secret_value="$(get_env_value "$secret_name")"
  path_value="$(get_env_value "$path_name")"

  if [[ -z "$secret_value" && -z "$path_value" ]]; then
    missing+=("$secret_name or $path_name")
  fi
done

for secret_name in "${scalar_secrets[@]}"; do
  if [[ -z "$(get_env_value "$secret_name")" ]]; then
    missing+=("$secret_name")
  fi
done

if [[ "${#missing[@]}" -ne 0 ]]; then
  echo "Missing required environment variables for $repo:" >&2
  for name in "${missing[@]}"; do
    echo "  - $name" >&2
  done
  exit 1
fi

echo "Configuring release secrets for $repo"

for spec in "${p12_secret_specs[@]}"; do
  secret_name="${spec%%:*}"
  path_name="${spec##*:}"
  secret_value="$(get_env_value "$secret_name")"

  if [[ -z "$secret_value" ]]; then
    secret_value="$(encode_base64_file "$(get_env_value "$path_name")")"
  fi

  set_secret "$secret_name" "$secret_value"
done

for secret_name in "${scalar_secrets[@]}"; do
  set_secret "$secret_name" "$(get_env_value "$secret_name")"
done

echo "Verifying release prerequisites"
bash "$(dirname "$0")/check-release-prereqs.sh" "$repo"
