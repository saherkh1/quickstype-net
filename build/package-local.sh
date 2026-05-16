#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-0.99.0}"
CHANNEL="${2:-canary}"
RID="${3:-}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ -z "$RID" ]]; then
  case "$(uname -s)" in
    Darwin) RID="osx-arm64" ;;
    *) echo "Unsupported host for package-local.sh. Pass a RID explicitly, for example: osx-arm64" >&2; exit 2 ;;
  esac
fi

if ! command -v vpk >/dev/null 2>&1; then
  echo "Velopack CLI 'vpk' was not found. Install it first: dotnet tool install -g vpk --version 0.0.1298" >&2
  exit 127
fi

export DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}"
if [[ -z "${DOTNET_ROOT:-}" ]] && [[ -d /opt/homebrew/opt/dotnet/libexec ]]; then
  export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
fi

PUBLISH_DIR="$ROOT_DIR/artifacts/publish/$RID/$VERSION"
OUTPUT_DIR="$ROOT_DIR/artifacts/velopack/$RID/$VERSION"
PROJECT="$ROOT_DIR/src/QuickSType.UI/QuickSType.UI.csproj"
MAIN_EXE="QuickSType"
PUBLISH_AOT="${QUICKSTYPE_PACKAGE_AOT:-true}"

if [[ "$RID" == win-* ]]; then
  MAIN_EXE="QuickSType.exe"
fi

rm -rf "$PUBLISH_DIR" "$OUTPUT_DIR"
mkdir -p "$PUBLISH_DIR" "$OUTPUT_DIR"

dotnet publish "$PROJECT" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishAot="$PUBLISH_AOT" \
  -p:Version="$VERSION" \
  -p:InformationalVersion="$VERSION" \
  -o "$PUBLISH_DIR" \
  --nologo

vpk pack \
  --packId QuickSType \
  --packTitle QuickSType \
  --packVersion "$VERSION" \
  --channel "$CHANNEL" \
  --runtime "$RID" \
  --packDir "$PUBLISH_DIR" \
  --mainExe "$MAIN_EXE" \
  --outputDir "$OUTPUT_DIR"

echo "Velopack artifacts written to $OUTPUT_DIR"
