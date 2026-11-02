#!/usr/bin/env python3
"""Fail when docs/ hardcodes an SDK version or a version tag.

The docs site is latest-only: the version comes from the latest GitHub Release at build time.
Write %SDK_VERSION% (for example 1.6.0) or %SDK_TAG% (v1.6.0) instead; docs/.vitepress/config.mts
replaces them in prose and code blocks.

Flags in authored pages (generated pages are skipped):
  - the current and every released SDK version (from CHANGELOG.md and Curated.Sdk.csproj)
  - any v1.2.3 style tag, and #v1.2.3 Git URL fragments
Usage: python3 scripts/check-docs-versions.py
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DOCS = ROOT / "docs"
GENERATED = {"changelog.md"}
GENERATED_DIRS = {"reference", "node_modules", ".vitepress/cache", ".vitepress/dist"}
SUFFIXES = {".md", ".vue", ".ts", ".mts", ".js", ".mjs", ".json"}
IGNORED_FILES = {"package.json", "package-lock.json", ".vitepress/generated.json"}


def known_versions() -> set[str]:
    versions = set(re.findall(r"^## \[(\d+\.\d+\.\d+)\]", (ROOT / "CHANGELOG.md").read_text(encoding="utf-8"), re.M))
    csproj = (ROOT / "Curated.Sdk/Curated.Sdk.csproj").read_text(encoding="utf-8")
    versions.update(re.findall(r"<Version>(\d+\.\d+\.\d+)</Version>", csproj))
    return versions


def authored_files() -> list[Path]:
    files = []
    for path in DOCS.rglob("*"):
        rel = path.relative_to(DOCS).as_posix()
        if not path.is_file() or path.suffix not in SUFFIXES:
            continue
        if rel in GENERATED or rel in IGNORED_FILES or any(rel == d or rel.startswith(d + "/") for d in GENERATED_DIRS):
            continue
        files.append(path)
    return sorted(files)


def main() -> int:
    versions = known_versions()
    version_re = re.compile(
        r"(?<![\w.])v?(?:" + "|".join(re.escape(v) for v in sorted(versions, reverse=True)) + r")(?![\w.])"
    )
    tag_re = re.compile(r"(?<![\w.])v\d+\.\d+\.\d+(?![\w.])")
    failed = False
    files = authored_files()
    for path in files:
        for no, line in enumerate(path.read_text(encoding="utf-8").split("\n"), 1):
            hits = set(version_re.findall(line)) | set(tag_re.findall(line))
            for hit in sorted(hits):
                failed = True
                rel = path.relative_to(ROOT).as_posix()
                print(f"FAIL {rel}:{no}: hardcoded SDK version `{hit}`", file=sys.stderr)
    if failed:
        print(
            "\nUse %SDK_VERSION% or %SDK_TAG% instead. The build replaces them with the latest release.",
            file=sys.stderr,
        )
        return 1
    print(f"ok   no hardcoded SDK versions in {len(files)} docs files ({len(versions)} known versions)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
