#!/usr/bin/env python3
"""Keep a Changelog helpers for curated-sdk releases.

- Ensures CHANGELOG.md has a section for the version being released.
- Prefer promoting a non-empty [Unreleased] section; otherwise generate from
  git commits via git-cliff (cliff.toml).
- Emits GitHub Release notes (changelog section + install boilerplate).
"""

from __future__ import annotations

import argparse
import datetime as dt
import os
import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_CHANGELOG = REPO_ROOT / "CHANGELOG.md"
COMPARE_BASE = "https://github.com/CuratedGames/curated-sdk"

SECTION_HEADER_RE = re.compile(
    r"^## \[([^\]]+)\](?:\s*-\s*(\d{4}-\d{2}-\d{2}))?\s*$", re.MULTILINE
)
LINK_LINE_RE = re.compile(r"^\[([^\]]+)\]:\s+\S+\s*$", re.MULTILINE)


def read_csproj_version(csproj: Path) -> str:
    text = csproj.read_text(encoding="utf-8")
    match = re.search(r"<Version>([^<]+)</Version>", text)
    if not match:
        raise SystemExit(f"No <Version> in {csproj}")
    return match.group(1).strip()


def split_changelog(text: str) -> tuple[str, str, str]:
    """Return (header_through_title, body, footer_links)."""
    lines = text.splitlines(keepends=True)
    body_start = None
    footer_start = None
    for i, line in enumerate(lines):
        if body_start is None and line.startswith("## "):
            body_start = i
        if body_start is not None and LINK_LINE_RE.match(line):
            # Footer starts at the first [label]: URL after sections.
            # Allow a blank line immediately before the link block.
            footer_start = i
            j = i - 1
            while j >= body_start and lines[j].strip() == "":
                footer_start = j
                j -= 1
            break
    if body_start is None:
        return text if text.endswith("\n") or text == "" else text + "\n", "", ""
    header = "".join(lines[:body_start])
    if footer_start is None:
        body = "".join(lines[body_start:])
        footer = ""
    else:
        body = "".join(lines[body_start:footer_start])
        footer = "".join(lines[footer_start:])
    return header, body, footer


def iter_sections(body: str) -> list[tuple[str, str | None, str]]:
    """Parse body into (title, date_or_none, section_text_including_header)."""
    matches = list(SECTION_HEADER_RE.finditer(body))
    if not matches:
        return []
    sections: list[tuple[str, str | None, str]] = []
    for idx, match in enumerate(matches):
        start = match.start()
        end = matches[idx + 1].start() if idx + 1 < len(matches) else len(body)
        title = match.group(1)
        date = match.group(2)
        sections.append((title, date, body[start:end]))
    return sections


def extract_section(text: str, version: str) -> str | None:
    _, body, _ = split_changelog(text)
    for title, _date, section in iter_sections(body):
        if title == version:
            return section.strip() + "\n"
    return None


def unreleased_is_empty(section_text: str) -> bool:
    """True when Unreleased has no bullet items."""
    lines = []
    for line in section_text.splitlines():
        if line.startswith("## "):
            continue
        if line.startswith("### "):
            continue
        if line.strip() == "":
            continue
        lines.append(line)
    return not any(line.lstrip().startswith("- ") for line in lines)


def promote_unreleased(section_text: str, version: str, date: str) -> str:
    promoted = SECTION_HEADER_RE.sub(
        f"## [{version}] - {date}", section_text, count=1
    )
    return promoted.strip() + "\n"


def empty_unreleased() -> str:
    return "## [Unreleased]\n\n"


def rebuild_compare_links(versions_newest_first: list[str]) -> str:
    """Build Keep a Changelog compare links. versions_newest_first includes Unreleased skip."""
    lines: list[str] = []
    # Always point Unreleased at the newest tagged version when present.
    tagged = [v for v in versions_newest_first if v != "Unreleased"]
    if tagged:
        lines.append(f"[Unreleased]: {COMPARE_BASE}/compare/v{tagged[0]}...HEAD")
    for idx, version in enumerate(tagged):
        if idx + 1 < len(tagged):
            prev = tagged[idx + 1]
            lines.append(
                f"[{version}]: {COMPARE_BASE}/compare/v{prev}...v{version}"
            )
        else:
            lines.append(f"[{version}]: {COMPARE_BASE}/releases/tag/v{version}")
    return "\n".join(lines) + ("\n" if lines else "")


def insert_version_section(text: str, new_section: str, version: str) -> str:
    header, body, _footer = split_changelog(text)
    sections = iter_sections(body)

    # Drop existing Unreleased and any stale copy of this version.
    kept: list[tuple[str, str | None, str]] = []
    for title, date, section in sections:
        if title == "Unreleased":
            continue
        if title == version:
            continue
        kept.append((title, date, section))

    ordered_titles = ["Unreleased", version] + [t for t, _, _ in kept]
    parts = [empty_unreleased().rstrip(), new_section.strip()]
    parts.extend(section.strip() for _title, _date, section in kept)
    new_body = "\n\n".join(parts) + "\n"

    footer = rebuild_compare_links(ordered_titles)
    if not header.endswith("\n\n"):
        header = header.rstrip("\n") + "\n\n"
    return header + new_body.rstrip() + "\n\n" + footer


def run_git_cliff(version: str, repo: Path) -> str:
    tag = f"v{version}"
    cmd = [
        "git",
        "cliff",
        "--config",
        str(repo / "cliff.toml"),
        "--tag",
        tag,
        "--unreleased",
    ]
    # When the tag already exists (re-running a release), generate from that tag range.
    existing = subprocess.run(
        ["git", "tag", "-l", tag],
        cwd=repo,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    if existing:
        cmd = [
            "git",
            "cliff",
            "--config",
            str(repo / "cliff.toml"),
            "--tag",
            tag,
            "--latest",
        ]
    try:
        proc = subprocess.run(
            cmd,
            cwd=repo,
            check=True,
            capture_output=True,
            text=True,
        )
    except FileNotFoundError as exc:
        raise SystemExit(
            "git-cliff not found on PATH (install: https://github.com/orhun/git-cliff)"
        ) from exc
    except subprocess.CalledProcessError as exc:
        sys.stderr.write(exc.stderr or exc.stdout or str(exc))
        raise SystemExit(exc.returncode) from exc
    out = proc.stdout.strip()
    if not out:
        # Fallback empty changed note so the release still has a section.
        today = dt.date.today().isoformat()
        return (
            f"## [{version}] - {today}\n\n"
            f"### Changed\n\n"
            f"- Release {version}\n"
        )
    return out + "\n"


def install_boilerplate(version: str) -> str:
    return f"""## Install

Download assets from this Release page (not nuget.org).

- Unity (recommended): `"com.curatedgames.sdk": "https://github.com/CuratedGames/curated-sdk.git?path=/engines/unity/com.curatedgames.sdk#v{version}"` in Packages/manifest.json.
- Unity (optional tarball): `com.curatedgames.sdk-{version}.tgz` via Add package from tarball.
- Godot 4: unzip `curated-godot-{version}.zip` into the project root and enable the plugin.
- Unreal Engine 5: unzip `curated-unreal-{version}.zip` into the project's `Plugins/` folder.
- RPG Maker MZ/MV: copy `CuratedSDK.js` into `js/plugins/`.
- .NET: add a local NuGet source for `Curated.Sdk.{version}.nupkg`, or reference `Curated.Sdk.dll`.
- Other engines: implement `launch-contract.md` and check it against `conformance.v1.json`.
- CLI: `curated-content-builder-windows-x64.exe` (login, list-depots, upload). See Agents.md.

License: see LICENSE.md (Curated SDK License). Source-available, not open source.
"""


def build_release_notes(section: str, version: str) -> str:
    # Drop the ## [version] header from the section for a cleaner GH release title context;
    # keep group headings and bullets. Softprops already sets the release name.
    lines = section.strip().splitlines()
    if lines and lines[0].startswith("## "):
        lines = lines[1:]
    body = "\n".join(lines).strip()
    parts = [
        f"**Curated.Sdk** {version}",
        "",
        body if body else "### Changed\n\n- Release notes unavailable.",
        "",
        install_boilerplate(version).rstrip(),
        "",
    ]
    return "\n".join(parts)


def ensure_changelog_for_version(
    text: str,
    version: str,
    *,
    today: str | None = None,
    cliff_section: str | None = None,
    repo: Path = REPO_ROOT,
) -> tuple[str, str, bool]:
    """Return (new_changelog, release_section, changed)."""
    existing = extract_section(text, version)
    if existing is not None:
        return text, existing, False

    date = today or dt.date.today().isoformat()
    _, body, _ = split_changelog(text)
    sections = {title: section for title, _date, section in iter_sections(body)}
    unreleased = sections.get("Unreleased", empty_unreleased())

    if not unreleased_is_empty(unreleased):
        release_section = promote_unreleased(unreleased, version, date)
    else:
        release_section = cliff_section if cliff_section is not None else run_git_cliff(
            version, repo
        )
        # Normalize date/header if cliff omitted them.
        if not release_section.lstrip().startswith("## "):
            release_section = f"## [{version}] - {date}\n\n{release_section.lstrip()}"
        else:
            release_section = promote_unreleased(release_section, version, date)

    updated = insert_version_section(text, release_section, version)
    return updated, extract_section(updated, version) or release_section, True


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--version",
        help="Version being released (default: Curated.Sdk.csproj <Version>)",
    )
    parser.add_argument(
        "--changelog",
        type=Path,
        default=DEFAULT_CHANGELOG,
        help="Path to CHANGELOG.md",
    )
    parser.add_argument(
        "--write-notes",
        type=Path,
        required=True,
        help="Write GitHub Release notes to this path",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Do not write CHANGELOG.md even when it would change",
    )
    args = parser.parse_args(argv)

    version = args.version or read_csproj_version(
        REPO_ROOT / "Curated.Sdk" / "Curated.Sdk.csproj"
    )
    text = args.changelog.read_text(encoding="utf-8")
    updated, section, changed = ensure_changelog_for_version(text, version)
    args.write_notes.write_text(build_release_notes(section, version), encoding="utf-8")
    if changed and not args.dry_run:
        args.changelog.write_text(updated, encoding="utf-8")
        print(f"updated {args.changelog} for {version}")
    elif changed:
        print(f"would update {args.changelog} for {version} (dry-run)")
    else:
        print(f"{args.changelog} already has [{version}]")
    print(f"wrote release notes to {args.write_notes}")

    # Keep engine package CHANGELOG.md files aligned with the root changelog
    # (Unity Package Manager and other engine package UIs read those copies).
    from engine_changelogs import sync_engine_changelogs

    # dry-run still renders against the would-be root text without writing.
    engine_results = sync_engine_changelogs(
        updated if changed else text,
        write=not args.dry_run,
    )
    engine_changed = any(c for _p, _t, c in engine_results.values())
    for engine, (path, _text, eng_changed) in engine_results.items():
        rel = path.relative_to(REPO_ROOT)
        if args.dry_run:
            print(
                f"{'would update' if eng_changed else 'unchanged'} {rel} ({engine})"
            )
        else:
            print(f"{'updated' if eng_changed else 'unchanged'} {rel} ({engine})")

    any_changed = changed or engine_changed
    # Expose changed flag for the workflow.
    github_output = os.environ.get("GITHUB_OUTPUT")
    if github_output:
        with open(github_output, "a", encoding="utf-8") as fh:
            fh.write(f"changed={'true' if any_changed else 'false'}\n")
            fh.write(f"root_changed={'true' if changed else 'false'}\n")
            fh.write(f"engine_changed={'true' if engine_changed else 'false'}\n")
            fh.write(f"version={version}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
