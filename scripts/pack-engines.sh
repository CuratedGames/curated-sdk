#!/usr/bin/env bash
# Package the engine bindings in engines/ as release assets.
# Usage: pack-engines.sh <path to netstandard2.1 Curated.Sdk.dll> [out dir]
set -euo pipefail

SDK_DIR="$(cd "$(dirname "$0")/.." && pwd)"
DLL="${1:?path to the netstandard2.1 Curated.Sdk.dll}"
OUT_DIR="${2:-$SDK_DIR/artifacts/engines}"
VERSION="$(grep -oP '(?<=<Version>)[^<]+' "$SDK_DIR/Curated.Sdk/Curated.Sdk.csproj" | head -1)"

mkdir -p "$OUT_DIR"

python3 - "$SDK_DIR" "$DLL" "$OUT_DIR" "$VERSION" <<'PY'
import io, json, shutil, sys, tarfile, zipfile
from pathlib import Path

sdk, dll, out, version = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]), sys.argv[4]
engines = sdk / "engines"
legal = [sdk / "LICENSE.md", sdk / "contracts" / "launch-contract.md"]

def files_under(root):
    return sorted(p for p in root.rglob("*") if p.is_file())

# Unity: UPM tarball (package/ prefix), DLL injected next to its committed .meta.
unity = engines / "unity" / "com.curatedgames.sdk"
manifest = json.loads((unity / "package.json").read_text(encoding="utf-8"))
if manifest["version"] != version:
    sys.exit(f"engines/unity package.json version {manifest['version']} != Curated.Sdk {version}")
tgz = out / f"com.curatedgames.sdk-{version}.tgz"
with tarfile.open(tgz, "w:gz") as tar:
    for path in files_under(unity):
        if path.name in ("Curated.Sdk.dll", "LICENSE.md"):
            continue
        tar.add(path, f"package/{path.relative_to(unity).as_posix()}")
    tar.add(dll, "package/Plugins/Curated.Sdk.dll")
    tar.add(sdk / "LICENSE.md", "package/LICENSE.md")
print(tgz)

def zip_tree(name, root, prefix):
    target = out / name
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
        for path in files_under(root):
            z.write(path, f"{prefix}/{path.relative_to(root).as_posix()}")
        for doc in legal:
            z.write(doc, f"{prefix}/{doc.name}")
    print(target)

zip_tree(f"curated-godot-{version}.zip", engines / "godot" / "addons" / "curated", "addons/curated")
zip_tree(f"curated-unreal-{version}.zip", engines / "unreal" / "CuratedSDK", "CuratedSDK")

plugin = out / "CuratedSDK.js"
shutil.copyfile(engines / "rpgmaker" / "CuratedSDK.js", plugin)
print(plugin)
PY
