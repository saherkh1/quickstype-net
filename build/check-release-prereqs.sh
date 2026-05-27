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

signing_mode="${QUICKSTYPE_RELEASE_SIGNING_MODE:-signed}"
if [[ "$signing_mode" != "signed" && "$signing_mode" != "unsigned" ]]; then
  echo "QUICKSTYPE_RELEASE_SIGNING_MODE must be signed or unsigned: $signing_mode" >&2
  exit 64
fi

required_workflows=("release" "secret-scan")
required_secrets=(
  APPLE_DEVELOPER_ID_APPLICATION_P12_BASE64
  APPLE_DEVELOPER_ID_APPLICATION_PASSWORD
  APPLE_DEVELOPER_ID_INSTALLER_P12_BASE64
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

echo "Checking release prerequisites for $repo"
echo "signing mode: $signing_mode"

workflow_names="$(gh workflow list --repo "$repo" --json name -q '.[].name')"
missing=0

for workflow in "${required_workflows[@]}"; do
  if grep -Fxq "$workflow" <<<"$workflow_names"; then
    echo "workflow: $workflow present"
  else
    echo "workflow: $workflow missing"
    missing=1
  fi
done

if [[ "$signing_mode" == "signed" ]]; then
  secret_names="$(gh secret list --repo "$repo" --json name -q '.[].name')"
  for secret in "${required_secrets[@]}"; do
    if grep -Fxq "$secret" <<<"$secret_names"; then
      echo "secret: $secret present"
    else
      echo "secret: $secret missing"
      missing=1
    fi
  done
else
  echo "Skipping signing secret checks for unsigned release mode."
fi

if [[ "$missing" -ne 0 ]]; then
  echo "Release prerequisites are incomplete." >&2
  exit 1
fi

echo "Release prerequisites are present."
