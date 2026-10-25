#!/usr/bin/env python3
"""Check that every binding embeds the game language table of contracts/fixtures/game-language.v1.json.

Godot and Unreal tests only run inside their engines, so CI runs this to catch drift in their
tables (and in the C# and RPG Maker ones) without an engine install.
Usage: python3 scripts/check-game-languages.py
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
fixture = json.loads((ROOT / "contracts/fixtures/game-language.v1.json").read_text(encoding="utf-8"))
expected = [(l["code"], l["name"], l["steam"], tuple(l["aliases"])) for l in fixture["languages"]]


def rows(path, row_pattern, alias_pattern):
    text = (ROOT / path).read_text(encoding="utf-8")
    found = []
    for m in re.finditer(row_pattern, text):
        aliases = tuple(re.findall(alias_pattern, m.group(4) or ""))
        found.append((m.group(1), m.group(2), m.group(3), aliases))
    return found


bindings = {
    "Curated.Sdk/GameLanguages.cs": rows(
        "Curated.Sdk/GameLanguages.cs",
        r'new\("([^"]+)", "([^"]+)", "([^"]+)"((?:, "[^"]+")*)\)',
        r'"([^"]+)"',
    ),
    "engines/godot/addons/curated/curated_contract.gd": rows(
        "engines/godot/addons/curated/curated_contract.gd",
        r'\["([^"]+)", "([^"]+)", "([^"]+)", \[([^\]]*)\]\]',
        r'"([^"]+)"',
    ),
    "engines/rpgmaker/CuratedSDK.js": rows(
        "engines/rpgmaker/CuratedSDK.js",
        r'\["([^"]+)", "([^"]+)", "([^"]+)", \[([^\]]*)\]\]',
        r'"([^"]+)"',
    ),
    "engines/unreal/CuratedSDK/Source/CuratedSDK/Private/CuratedLaunchContract.cpp": rows(
        "engines/unreal/CuratedSDK/Source/CuratedSDK/Private/CuratedLaunchContract.cpp",
        r'\{ TEXT\("([^"]+)"\), TEXT\("([^"]+)"\), TEXT\("([^"]+)"\), \{([^}]*)\} \}',
        r'TEXT\("([^"]+)"\)',
    ),
}

failed = False
for path, found in bindings.items():
    if found != expected:
        failed = True
        print(f"FAIL {path}: game language table differs from the fixture", file=sys.stderr)
        for want, got in zip(expected, found):
            if want != got:
                print(f"  expected {want}, got {got}", file=sys.stderr)
        if len(found) != len(expected):
            print(f"  expected {len(expected)} rows, got {len(found)}", file=sys.stderr)
    else:
        print(f"ok   {path} ({len(found)} languages)")

# The Unreal automation test hard-codes the fixture cases; keep them in step.
ue_test = (ROOT / "engines/unreal/CuratedSDK/Source/CuratedSDK/Private/Tests/CuratedLaunchContractTests.cpp").read_text(encoding="utf-8")
ue_cases = re.findall(r'\{ TEXT\("([^"]*)"\), (true|false), TEXT\("([^"]+)"\), TEXT\("([^"]+)"\) \}', ue_test)
want_cases = [
    (c["env"].get("CURATED_LANGUAGE", ""), "true" if "CURATED_LANGUAGE" in c["env"] else "false", c["expect"]["language"], c["expect"]["steam"])
    for c in fixture["cases"]
]
if ue_cases != want_cases:
    failed = True
    print("FAIL Unreal GameLanguage automation test cases differ from the fixture", file=sys.stderr)
else:
    print(f"ok   Unreal automation test ({len(ue_cases)} cases)")

sys.exit(1 if failed else 0)
