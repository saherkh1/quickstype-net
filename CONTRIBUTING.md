# Contributing

Thanks for helping improve QuickSType.

## Development Setup

1. Install the .NET 10 SDK.
2. Restore and build:

```bash
dotnet restore
dotnet build
```

3. Run tests:

```bash
dotnet test QuickSType.sln
```

## Pull Requests

- Keep changes focused and explain the user-visible behavior.
- Add or update tests for behavior changes.
- Run the full test suite before opening a PR.
- Do not commit model binaries, logs, local package artifacts, signing material, Sentry DSNs, or other secrets.
- For AOT-sensitive code, run:

```bash
dotnet build src/QuickSType.UI/QuickSType.UI.csproj -warnaserror:IL2026,IL3050
```

## Manual Testing

Changes touching global hotkeys, Accessibility permissions, tray anchoring, update installers, or text injection need manual evidence in `tests/manual/`.

## Style

- Prefer existing project patterns over new abstractions.
- Keep platform-specific code in `QuickSType.Platform.Mac` or `QuickSType.Platform.Windows`.
- Keep core contracts in `QuickSType.Core` when they are shared by both platforms.
