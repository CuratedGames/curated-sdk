#!/usr/bin/env python3
"""Fail when a docs page imports a code snippet that no longer exists.

VitePress `<<< @/path#region` imports do not fail the build when the file or region is gone:
a missing file renders an error text, and a missing region silently imports the whole file.
This check makes both fail CI.

With --dist, also checks the built site: no unreplaced %SDK_VERSION% / %SDK_TAG% tokens and no
"Code snippet path not found" placeholders.
Usage: python3 scripts/check-docs-snippets.py [--dist docs/.vitepress/dist]
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
IMPORT = re.compile(r"^<<<\s+@/(\S+?)(?:#([\w-]+))?(?:\{[^}]*\})?\s*$")


def region_ok(text: str, name: str) -> bool:
    start = re.search(rf"(?:#|//\s*#|<!--\s*#)region {re.escape(name)}\b", text)
    end = re.search(rf"#endregion(?: {re.escape(name)}\b|\s*$)", text[start.end():] if start else "", re.M)
    return bool(start and end)


def main() -> int:
    failed = False
    count = 0
    for page in sorted(DOCS.rglob("*.md")):
        if "node_modules" in page.parts:
            continue
        for no, line in enumerate(page.read_text(encoding="utf-8").split("\n"), 1):
            m = IMPORT.match(line.strip())
            if not m:
                continue
            count += 1
            rel = page.relative_to(ROOT).as_posix()
            target = (DOCS / m.group(1)).resolve()
            if not target.is_file():
                failed = True
                print(f"FAIL {rel}:{no}: snippet file not found: {m.group(1)}", file=sys.stderr)
            elif m.group(2) and not region_ok(target.read_text(encoding="utf-8"), m.group(2)):
                failed = True
                print(f"FAIL {rel}:{no}: region `{m.group(2)}` not found in {m.group(1)}", file=sys.stderr)
    if "--dist" in sys.argv:
        dist = ROOT / sys.argv[sys.argv.index("--dist") + 1]
        html = list(dist.rglob("*.html"))
        if not html:
            failed = True
            print(f"FAIL no HTML in {dist}", file=sys.stderr)
        for path in html:
            text = path.read_text(encoding="utf-8")
            for token in ("%SDK_VERSION%", "%SDK_TAG%", "%25SDK_VERSION%25", "%25SDK_TAG%25", "Code snippet path not found"):
                if token in text:
                    failed = True
                    print(f"FAIL {path.relative_to(ROOT).as_posix()}: contains `{token}`", file=sys.stderr)
        if not failed:
            print(f"ok   built site: {len(html)} pages, no unreplaced version tokens or missing snippets")
    if failed:
        print("\nFix the import path or add the `#region name` / `#endregion name` markers back.", file=sys.stderr)
        return 1
    print(f"ok   {count} snippet imports resolve to existing files and regions")
    return 0


if __name__ == "__main__":
    sys.exit(main())
