# Security Policy

## Supported Versions

Until v1.0.0 is published, security fixes are handled on `main`.

After v1.0.0, the latest minor release line is supported.

## Reporting a Vulnerability

Please do not open a public issue for security vulnerabilities.

Use GitHub private vulnerability reporting when the repository is public. If that is unavailable, contact the maintainer directly and include:

- Affected version or commit.
- Platform and install method.
- Reproduction steps.
- Impact and any known workaround.

## Secrets

Do not include Sentry DSNs, Apple signing material, Azure signing credentials, model hosting credentials, or private logs in issues or pull requests.

The repository runs gitleaks and TruffleHog scans in CI.
