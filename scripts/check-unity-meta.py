#!/usr/bin/env python3
"""Fail when a Unity package under engines/unity has a broken .meta file set.

Unity ignores an asset whose .meta has a malformed GUID ("does not have a valid GUID and its
corresponding Asset file will be ignored"), so a hand-typed GUID ships a package that fails to
import. For every package (a folder with package.json) under engines/unity this checks that:

- every .meta has a `guid:` of exactly 32 lowercase hex characters,
- no GUID is used twice,
- every file and folder in the package has a .meta (the package root itself does not),
- every .meta has its asset or folder next to it.

Files and folders Unity skips on import (names starting with "." or ending with "~") are
ignored.

Generate a GUID with: python3 -c "import uuid;print(uuid.uuid4().hex)"

Usage:
  python3 scripts/check-unity-meta.py
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UNITY_DIR = ROOT / "engines" / "unity"
GUID_LINE_RE = re.compile(r"^guid:\s*(\S*)\s*$", re.MULTILINE)
GUID_RE = re.compile(r"^[0-9a-f]{32}$")


def unity_skips(path: Path, package: Path) -> bool:
    parts = path.relative_to(package).parts
    return any(p.startswith(".") or p.endswith("~") for p in parts)


def check_package(package: Path, seen: dict[str, Path]) -> list[str]:
    errors: list[str] = []
    for path in sorted(package.rglob("*")):
        if unity_skips(path, package):
            continue
        rel = path.relative_to(ROOT).as_posix()
        if path.name.endswith(".meta") and path.is_file():
            asset = path.with_name(path.name[: -len(".meta")])
            if not asset.exists():
                errors.append(f"orphan .meta, no asset or folder next to it: {rel}")
            match = GUID_LINE_RE.search(path.read_text(encoding="utf-8"))
            if not match:
                errors.append(f"no guid line: {rel}")
                continue
            guid = match.group(1)
            if not GUID_RE.match(guid):
                errors.append(f"guid is not 32 lowercase hex characters ({guid!r}): {rel}")
                continue
            if guid in seen:
                errors.append(
                    f"duplicate guid {guid}: {rel} and {seen[guid].relative_to(ROOT).as_posix()}"
                )
            else:
                seen[guid] = path
            continue
        meta = path.with_name(path.name + ".meta")
        if not meta.is_file():
            errors.append(f"missing .meta: {rel}")
    return errors


def main() -> int:
    packages = sorted(p.parent for p in UNITY_DIR.glob("*/package.json"))
    if not packages:
        print(f"FAIL no Unity package found under {UNITY_DIR.relative_to(ROOT)}", file=sys.stderr)
        return 1
    seen: dict[str, Path] = {}
    failed = False
    for package in packages:
        errors = check_package(package, seen)
        name = package.relative_to(ROOT).as_posix()
        if errors:
            failed = True
            print(f"FAIL {name}: Unity .meta files are invalid", file=sys.stderr)
            for error in errors:
                print(f"  {error}", file=sys.stderr)
        else:
            print(f"OK {name}")
    if failed:
        print(
            'Fix: give each asset a .meta with a fresh guid '
            '(python3 -c "import uuid;print(uuid.uuid4().hex)") and remove orphan .meta files.',
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
