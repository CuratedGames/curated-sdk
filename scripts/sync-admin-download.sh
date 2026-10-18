#!/usr/bin/env bash
# Deprecated: admin Documentation downloads now point at curated-sdk GitHub Releases.
# This script only verifies a local pack still builds (no copy into apps/admin/public).
set -euo pipefail

SDK_DIR="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${1:-$SDK_DIR/artifacts/nuget}"

echo "NOTE: Canonical downloads are https://github.com/CuratedGames/curated-sdk/releases"
mkdir -p "$OUT_DIR"
dotnet pack "$SDK_DIR/Curated.Sdk/Curated.Sdk.csproj" --configuration Release -o "$OUT_DIR"
bash "$SDK_DIR/scripts/pack-unity-zip.sh" "${OUT_DIR}/../sdk-unity"
echo "Local pack OK under $OUT_DIR (not synced to admin public/downloads)."
