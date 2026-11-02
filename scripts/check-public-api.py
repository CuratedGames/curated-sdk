#!/usr/bin/env python3
"""Fail when a binding's public API and contracts/public-api.v1.json disagree.

Every public type, method, property, enum value, and plugin parameter of every binding must
have an entry with a description in the manifest, and every manifest entry must still exist in
the binding source. The docs site builds its API reference pages from the manifest, so this
check keeps those pages complete and free of removed members.

Usage:
  python3 scripts/check-public-api.py          # check (CI)
  python3 scripts/check-public-api.py --write  # add missing entries, drop stale ones

--write keeps existing descriptions, seeds new ones from the source doc comment, and writes
"TODO" when there is none. The check fails on TODO, so fill those in before pushing.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import public_api  # noqa: E402

MANIFEST = public_api.ROOT / "contracts/public-api.v1.json"


def load_manifest() -> dict:
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def main() -> int:
    write = "--write" in sys.argv[1:]
    manifest = load_manifest()
    bindings = manifest.setdefault("bindings", {})
    failed = False
    for name in public_api.BINDINGS:
        members = public_api.extract(name)
        found = [m.name for m in members]
        if len(set(found)) != len(found):
            dupes = sorted({n for n in found if found.count(n) > 1})
            print(f"FAIL {name}: extractor returned duplicates {dupes}", file=sys.stderr)
            failed = True
        listed: dict[str, str] = bindings.get(name, {})
        missing = [n for n in found if n not in listed]
        stale = [n for n in listed if n not in set(found)]
        empty = [n for n, d in listed.items() if n in set(found) and (not d.strip() or d.strip() == "TODO")]
        if write:
            docs = {m.name: m.doc for m in members}
            bindings[name] = {n: listed.get(n) or docs.get(n) or "TODO" for n in found}
            continue
        if missing or stale or empty:
            failed = True
            print(f"FAIL {name}: public API and contracts/public-api.v1.json differ", file=sys.stderr)
            for n in missing:
                print(f"  public in source, missing from manifest: {n}", file=sys.stderr)
            for n in stale:
                print(f"  in manifest, no longer public in source: {n}", file=sys.stderr)
            for n in empty:
                print(f"  description missing (empty or TODO): {n}", file=sys.stderr)
        else:
            print(f"ok   {name} ({len(found)} public members)")
    if write:
        MANIFEST.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        todo = sum(1 for b in bindings.values() for d in b.values() if d == "TODO")
        print(f"wrote {MANIFEST.relative_to(public_api.ROOT)} ({todo} TODO descriptions)")
        return 0
    if failed:
        print(
            "\nUpdate contracts/public-api.v1.json (run `python3 scripts/check-public-api.py --write`, "
            "then replace every TODO with a one-line description). The docs API reference is built from it.",
            file=sys.stderr,
        )
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
