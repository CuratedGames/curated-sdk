#!/usr/bin/env python3
"""Fail a pull request that changes a public API or a Content Builder flag without docs.

Compares the public surface at the PR base and head:
  - public members of every binding (scripts/public_api.py)
  - Content Builder commands, flags, and build script fields (scripts/docs_gen.py parsers)

When either changed, the PR must also change something under docs/ or
contracts/public-api.v1.json. Internal edits to the same files (bodies, private members,
comments) do not trigger it.

Override: add the `docs-not-needed` label to the pull request. The label is read from the
pull_request event payload (GITHUB_EVENT_PATH), so no token is needed.

Usage (CI): python3 scripts/check-docs-updated.py --base <sha> [--head <sha>]
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import tarfile
import tempfile
from io import BytesIO
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import docs_gen  # noqa: E402
import public_api  # noqa: E402

ROOT = public_api.ROOT
OVERRIDE_LABEL = "docs-not-needed"
DOC_PATHS = ("docs/", "contracts/public-api.v1.json")


def git(*args: str) -> bytes:
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True).stdout


def checkout(rev: str, dest: Path) -> None:
    """Extract only the files the surface parsers read, at `rev`."""
    paths = ["Curated.Sdk", "engines", "tooling/content-builder/src"]
    data = git("archive", "--format=tar", rev, "--", *paths)
    with tarfile.open(fileobj=BytesIO(data)) as tar:
        tar.extractall(dest, filter="data") if hasattr(tarfile, "data_filter") else tar.extractall(dest)


def surface(root: Path) -> dict:
    api = {
        name: sorted((m.name, tuple(m.signatures)) for m in public_api.extract(name, root))
        for name in public_api.BINDINGS
    }
    cli = []
    if (root / "tooling/content-builder/src/main.rs").exists():
        cli = [
            (c["name"], [(a["flag"], a["env"], a["default"], a["required"]) for a in c["args"]])
            for c in docs_gen.parse_cli(root)
        ]
    fields = []
    if (root / "tooling/content-builder/src/config.rs").exists():
        fields = [(label, [(n, t) for n, t, _ in f]) for label, f in docs_gen.parse_script_fields(root)]
    return {"api": api, "cli": cli, "script": fields}


def describe(before: dict, after: dict) -> list[str]:
    changes: list[str] = []
    for name in public_api.BINDINGS:
        b = dict(before["api"].get(name, []))
        a = dict(after["api"].get(name, []))
        for member in sorted(set(a) - set(b)):
            changes.append(f"{name}: added {member}")
        for member in sorted(set(b) - set(a)):
            changes.append(f"{name}: removed {member}")
        for member in sorted(set(a) & set(b)):
            if a[member] != b[member]:
                changes.append(f"{name}: signature changed {member}")
    if before["cli"] != after["cli"]:
        changes.append("content-builder: commands or flags changed")
    if before["script"] != after["script"]:
        changes.append("content-builder: build script fields changed")
    return changes


def pr_labels() -> list[str]:
    path = os.environ.get("GITHUB_EVENT_PATH")
    if not path or not Path(path).exists():
        return []
    event = json.loads(Path(path).read_text(encoding="utf-8"))
    return [l.get("name", "") for l in (event.get("pull_request") or {}).get("labels", [])]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", default="HEAD")
    args = parser.parse_args()

    changed = git("diff", "--name-only", f"{args.base}...{args.head}").decode().split()
    docs_touched = [p for p in changed if p.startswith(DOC_PATHS)]

    with tempfile.TemporaryDirectory() as tmp:
        base_dir = Path(tmp) / "base"
        head_dir = Path(tmp) / "head"
        base_dir.mkdir()
        head_dir.mkdir()
        checkout(args.base, base_dir)
        checkout(args.head, head_dir)
        changes = describe(surface(base_dir), surface(head_dir))

    if not changes:
        print("ok   no public API or Content Builder flag changes")
        return 0
    print("Public surface changes in this PR:")
    for c in changes:
        print(f"  - {c}")
    if docs_touched:
        print(f"ok   docs updated: {', '.join(docs_touched)}")
        return 0
    if OVERRIDE_LABEL in pr_labels():
        print(f"ok   `{OVERRIDE_LABEL}` label set, skipping the docs requirement")
        return 0
    print(
        "\nFAIL this PR changes a public SDK API or a Content Builder flag but does not touch docs/ or "
        "contracts/public-api.v1.json.\n"
        "  - Public API change: update contracts/public-api.v1.json "
        "(python3 scripts/check-public-api.py --write, then describe each new member) and any guide in docs/guide.\n"
        "  - Content Builder change: update docs/guide/content-builder.md.\n"
        f"  - Nothing to document? Add the `{OVERRIDE_LABEL}` label to the PR; this check re-runs on label changes.",
        file=sys.stderr,
    )
    return 1


if __name__ == "__main__":
    sys.exit(main())
