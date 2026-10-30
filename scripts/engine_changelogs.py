#!/usr/bin/env python3
"""Generate per-engine package CHANGELOG.md files from the root changelog.

Unity Package Manager (and other engine package UIs) read CHANGELOG.md inside the
package folder. This script filters the repository Keep a Changelog file so each
engine package only shows:

- entries that mention that engine, and
- shared / common SDK feature changes (C# API, launch contract, multi-engine work),

while omitting tooling-only noise (Content Builder CLI, depot upload mechanics, etc.)
unless the bullet also clearly affects that engine package.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

from release_changelog import (
    DEFAULT_CHANGELOG,
    REPO_ROOT,
    iter_sections,
    rebuild_compare_links,
    split_changelog,
)

CATEGORY_RE = re.compile(r"^###\s+(.+?)\s*$")
BULLET_RE = re.compile(r"^(\s*)-\s+")

ENGINE_PACKAGES: dict[str, dict[str, object]] = {
    "unity": {
        "title": "Curated SDK Unity package",
        "path": REPO_ROOT / "engines" / "unity" / "com.curatedgames.sdk" / "CHANGELOG.md",
        "meta": True,
        # Match engine name and Unity package identifiers.
        "patterns": (
            r"\bunity\b",
            r"\bupm\b",
            r"com\.curatedgames\.sdk",
            r"games\.curated\.sdk",
            r"curated\.unity",
            r"plugins/curated\.sdk\.dll",
        ),
    },
    "godot": {
        "title": "Curated SDK Godot addon",
        "path": REPO_ROOT / "engines" / "godot" / "addons" / "curated" / "CHANGELOG.md",
        "meta": False,
        "patterns": (r"\bgodot\b",),
    },
    "unreal": {
        "title": "Curated SDK Unreal plugin",
        "path": REPO_ROOT
        / "engines"
        / "unreal"
        / "CuratedSDK"
        / "CHANGELOG.md",
        "meta": False,
        "patterns": (r"\bunreal\b",),
    },
    "rpgmaker": {
        "title": "Curated SDK RPG Maker plugin",
        "path": REPO_ROOT / "engines" / "rpgmaker" / "CHANGELOG.md",
        "meta": False,
        "patterns": (r"\brpg\s*maker\b", r"\brpgmaker\b", r"curatedsdk\.js"),
    },
}

# Shared SDK / launch-contract surface — relevant to every engine package when no
# other-engine-only list applies.
SHARED_RE = re.compile(
    r"(?:"
    + r"|".join(
        (
            r"\bcurated\.sdk\b",
            r"\bcuratedclient\b",
            r"\bc#\s*sdk\b",
            r"\b\.net\b",
            r"\blaunch\s+contract\b",
            r"\bmincompatiblecontractversion\b",
            r"\bunsupported_contract_version\b",
            r"\bevery\s+engine\b",
            r"\ball\s+engines\b",
            r"\bmulti-engine\b",
            r"\bevery\s+engine\s+binding\b",
            r"\bevery\s+binding\b",
            r"\binitfromlauncher\b",
            r"\binitasync\b",
            r"\bisentitled\b",
            r"\bnot_entitled\b",
            r"\bentitlement\b",
            r"\bachievement",
            r"\bplayer\s+stats?\b",
            r"\bcloud\s+save\b",
            r"\bfriends?\b",
            r"\bpresence\b",
            r"\bopenid\b",
            r"\boverlay\s+toast\b",
            r"\bgetcurrentgamelanguage\b",
            r"\bcurated_id\b",
            r"\bpublic\s+api\b",
            r"\bcopyright\b",
            r"\btrademark\b",
        )
    )
    + r")",
    re.IGNORECASE,
)

# When a bullet names some engines but not this one, only keep it if it also
# clearly covers the whole SDK / all bindings (so Unity still gets "C# SDK, Godot…").
CROSS_ENGINE_SHARED_RE = re.compile(
    r"(?:"
    + r"|".join(
        (
            r"\bc#\s*sdk\b",
            r"\bcuratedclient\b",
            r"\bevery\s+engine\b",
            r"\ball\s+engines\b",
            r"\bmulti-engine\b",
            r"\bevery\s+engine\s+binding\b",
            r"\bevery\s+binding\b",
            r"\blaunch\s+contract\b",
        )
    )
    + r")",
    re.IGNORECASE,
)

# Tooling / Content Builder only — omit from engine packages unless also engine-tagged.
TOOLING_ONLY_RE = re.compile(
    r"(?:"
    + r"|".join(
        (
            r"\bcontent\s+builder\b",
            r"\bcurated-content-builder\b",
            r"\bdepot\s+upload",
            r"\bfile-level\s+cas\b",
            r"\bcas_files\b",
            r"\b\.cas-staging\b",
            r"--depot-id\b",
            r"--depot-key\b",
            r"--chunk-size\b",
            r"--concurrency\b",
            r"\bagents\.md\b",
            r"\bcli\s+tooling\b",
            r"\bcontent\s+builder\s+cli\b",
        )
    )
    + r")",
    re.IGNORECASE,
)

UNITY_META_TEMPLATE = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

# Stable GUID for engines/unity/com.curatedgames.sdk/CHANGELOG.md
UNITY_CHANGELOG_GUID = "a7c3e91f2b4d4e6a8f01c2d3e4f50617"


def package_header(title: str) -> str:
    return (
        f"# Changelog\n"
        f"\n"
        f"All notable changes to the {title} are documented in this file.\n"
        f"\n"
        f"The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),\n"
        f"and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).\n"
        f"\n"
        f"Generated from the repository root `CHANGELOG.md`: engine-specific and shared SDK\n"
        f"entries are kept; Content Builder and other tooling-only notes are omitted unless\n"
        f"they affect this package.\n"
        f"\n"
    )


def parse_categories(section_text: str) -> list[tuple[str | None, list[str]]]:
    """Split a version section into (category_or_None, bullet_blocks).

    Each bullet block is the full bullet line plus any continuation lines until the
    next bullet, category, or blank-line-separated structural break.
    """
    lines = section_text.splitlines()
    # Drop the ## header
    if lines and lines[0].startswith("## "):
        lines = lines[1:]

    categories: list[tuple[str | None, list[str]]] = []
    current_cat: str | None = None
    current_bullets: list[str] = []
    current_bullet_lines: list[str] = []

    def flush_bullet() -> None:
        nonlocal current_bullet_lines
        if current_bullet_lines:
            current_bullets.append("\n".join(current_bullet_lines).rstrip() + "\n")
            current_bullet_lines = []

    def flush_category() -> None:
        nonlocal current_cat, current_bullets
        flush_bullet()
        if current_cat is not None or current_bullets:
            categories.append((current_cat, current_bullets))
        current_cat = None
        current_bullets = []

    for line in lines:
        cat = CATEGORY_RE.match(line)
        if cat:
            flush_category()
            current_cat = cat.group(1).strip()
            continue
        if BULLET_RE.match(line):
            flush_bullet()
            current_bullet_lines = [line.rstrip()]
            continue
        if current_bullet_lines:
            # Continuation of a multi-line bullet (or blank line inside it).
            if line.strip() == "" and not any(
                ln.strip() for ln in current_bullet_lines[1:]
            ):
                # Single trailing blank after a bullet: end the bullet.
                flush_bullet()
            else:
                current_bullet_lines.append(line.rstrip())
            continue
        # Ignore stray blank lines between categories.
        if line.strip() == "":
            continue
        # Non-bullet prose under a section: treat as its own block (rare).
        flush_bullet()
        current_bullets.append(line.rstrip() + "\n")

    flush_category()
    return categories


def bullet_text(bullet: str) -> str:
    return BULLET_RE.sub("", bullet, count=1).strip()


def engine_mentioned(text: str, engine: str) -> bool:
    patterns = ENGINE_PACKAGES[engine]["patterns"]
    assert isinstance(patterns, tuple)
    return any(re.search(p, text, re.IGNORECASE) for p in patterns)


def mentioned_engines(text: str) -> set[str]:
    return {name for name in ENGINE_PACKAGES if engine_mentioned(text, name)}


def classify_bullet(bullet: str, engine: str) -> bool:
    """Return True when the bullet belongs in this engine package changelog."""
    text = bullet_text(bullet)
    if not text:
        return False

    named = mentioned_engines(text)
    if engine in named:
        return True

    tooling = TOOLING_ONLY_RE.search(text) is not None
    if tooling:
        return False

    # Named other engine(s) but not this one: keep only explicit cross-engine SDK notes.
    if named and engine not in named:
        return CROSS_ENGINE_SHARED_RE.search(text) is not None

    if SHARED_RE.search(text) is not None:
        return True

    # Default: non-tooling, non-engine-tagged notes are treated as shared project changes.
    return True


def filter_section(section_text: str, engine: str) -> str | None:
    """Return filtered section text (with header) or None if no bullets remain."""
    stripped = section_text.lstrip()
    match = re.match(r"^##\s+\[([^\]]+)\](.*)$", stripped, re.MULTILINE)
    if not match:
        return None
    title = match.group(1)
    first_line = stripped.splitlines()[0]

    kept_categories: list[tuple[str, list[str]]] = []
    for cat, bullets in parse_categories(section_text):
        kept = [b for b in bullets if classify_bullet(b, engine)]
        if kept and cat is not None:
            kept_categories.append((cat, kept))
        elif kept and cat is None:
            # Preserve uncategorized bullets under Changed.
            kept_categories.append(("Changed", kept))

    if title != "Unreleased" and not kept_categories:
        return None
    if title == "Unreleased" and not kept_categories:
        return "## [Unreleased]\n\n"

    parts = [first_line]
    for cat, bullets in kept_categories:
        parts.append("")
        parts.append(f"### {cat}")
        parts.append("")
        for bullet in bullets:
            parts.append(bullet.rstrip("\n"))
    return "\n".join(parts).rstrip() + "\n"


def render_engine_changelog(root_text: str, engine: str) -> str:
    cfg = ENGINE_PACKAGES[engine]
    title = str(cfg["title"])
    header = package_header(title)
    _, body, _ = split_changelog(root_text)
    sections = iter_sections(body)

    rendered: list[str] = []
    titles_for_links: list[str] = []
    for sec_title, _date, section in sections:
        filtered = filter_section(section, engine)
        if filtered is None:
            continue
        # Always keep Unreleased (may be empty).
        rendered.append(filtered.rstrip())
        titles_for_links.append(sec_title)

    if "Unreleased" not in titles_for_links:
        rendered.insert(0, "## [Unreleased]")
        titles_for_links.insert(0, "Unreleased")

    body_out = "\n\n".join(rendered) + "\n"
    footer = rebuild_compare_links(titles_for_links)
    # Footer links still point at the monorepo tags (same versions).
    return header + body_out.rstrip() + "\n\n" + footer


def write_unity_meta(changelog_path: Path) -> None:
    meta_path = Path(str(changelog_path) + ".meta")
    meta_path.write_text(
        UNITY_META_TEMPLATE.format(guid=UNITY_CHANGELOG_GUID),
        encoding="utf-8",
    )


def sync_engine_changelogs(
    root_text: str,
    *,
    engines: list[str] | None = None,
    write: bool = True,
) -> dict[str, tuple[Path, str, bool]]:
    """Render changelogs for engines. Returns path -> (path, text, changed)."""
    selected = engines or list(ENGINE_PACKAGES)
    results: dict[str, tuple[Path, str, bool]] = {}
    for engine in selected:
        if engine not in ENGINE_PACKAGES:
            raise SystemExit(f"unknown engine: {engine}")
        path = Path(str(ENGINE_PACKAGES[engine]["path"]))
        text = render_engine_changelog(root_text, engine)
        previous = path.read_text(encoding="utf-8") if path.is_file() else None
        changed = previous != text
        if write and changed:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding="utf-8")
            if ENGINE_PACKAGES[engine].get("meta"):
                write_unity_meta(path)
        results[engine] = (path, text, changed)
    return results


def check_engine_changelogs(root_text: str, engines: list[str] | None = None) -> int:
    results = sync_engine_changelogs(root_text, engines=engines, write=False)
    stale: list[str] = []
    for engine, (path, expected, _changed) in results.items():
        if not path.is_file():
            stale.append(f"missing {path.relative_to(REPO_ROOT)}")
            continue
        actual = path.read_text(encoding="utf-8")
        if actual != expected:
            stale.append(f"stale {path.relative_to(REPO_ROOT)}")
        meta = Path(str(path) + ".meta")
        if ENGINE_PACKAGES[engine].get("meta") and not meta.is_file():
            stale.append(f"missing {meta.relative_to(REPO_ROOT)}")
    if stale:
        for item in stale:
            print(f"engine changelog check failed: {item}", file=sys.stderr)
        print(
            "Run: python3 scripts/engine_changelogs.py --write",
            file=sys.stderr,
        )
        return 1
    print("engine changelogs are up to date")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--changelog",
        type=Path,
        default=DEFAULT_CHANGELOG,
        help="Path to root CHANGELOG.md",
    )
    parser.add_argument(
        "--engine",
        action="append",
        dest="engines",
        choices=sorted(ENGINE_PACKAGES),
        help="Limit to one or more engines (default: all)",
    )
    parser.add_argument(
        "--write",
        action="store_true",
        help="Write engine CHANGELOG.md files",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Exit non-zero if committed engine changelogs drift from the filter",
    )
    parser.add_argument(
        "--print",
        action="store_true",
        dest="print_out",
        help="Print rendered changelogs to stdout",
    )
    args = parser.parse_args(argv)

    if not args.write and not args.check and not args.print_out:
        parser.error("specify --write, --check, and/or --print")

    root_text = args.changelog.read_text(encoding="utf-8")

    if args.check:
        return check_engine_changelogs(root_text, engines=args.engines)

    results = sync_engine_changelogs(
        root_text, engines=args.engines, write=args.write
    )
    for engine, (path, text, changed) in results.items():
        rel = path.relative_to(REPO_ROOT)
        if args.write:
            print(f"{'updated' if changed else 'unchanged'} {rel}")
        if args.print_out:
            print(f"----- {engine}: {rel} -----")
            print(text, end="" if text.endswith("\n") else "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
