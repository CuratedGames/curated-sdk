#!/usr/bin/env bash
# Build Release Curated.Sdk.dll and zip it for Unity Plugins drop-in.
# Works from the monorepo (packages/sdk-csharp) or a dedicated SDK repo root.
set -euo pipefail

SDK_DIR="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${1:-$SDK_DIR/artifacts/sdk-unity}"
PROJ="$SDK_DIR/Curated.Sdk/Curated.Sdk.csproj"

VERSION="$(
  grep -oP '(?<=<Version>)[^<]+' "$PROJ" | head -1
)"

mkdir -p "$OUT_DIR"
dotnet publish "$PROJ" --configuration Release -o "$OUT_DIR/publish" --no-self-contained

ZIP_NAME="Curated.Sdk.${VERSION}.unity.zip"
STAGE="$OUT_DIR/stage"
rm -rf "$STAGE"
mkdir -p "$STAGE/Curated.Sdk"
cp -f "$OUT_DIR/publish/Curated.Sdk.dll" "$STAGE/Curated.Sdk/"
if [[ -f "$OUT_DIR/publish/Curated.Sdk.xml" ]]; then
  cp -f "$OUT_DIR/publish/Curated.Sdk.xml" "$STAGE/Curated.Sdk/"
fi

python3 - <<PY
import zipfile
from pathlib import Path
stage = Path("$STAGE")
out = Path("$OUT_DIR") / "$ZIP_NAME"
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    for path in stage.rglob("*"):
        if path.is_file():
            z.write(path, path.relative_to(stage).as_posix())
print(out)
PY

cp -f "$OUT_DIR/$ZIP_NAME" "$OUT_DIR/Curated.Sdk.unity.zip"
echo "Unity zip: $OUT_DIR/$ZIP_NAME"
