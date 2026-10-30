#!/usr/bin/env python3
"""Unit tests for scripts/engine_changelogs.py."""

from __future__ import annotations

import unittest

from engine_changelogs import (
    classify_bullet,
    filter_section,
    render_engine_changelog,
)

ROOT = """# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Content Builder uploads depot files via file-level CAS (zstd or identity blobs).

## [1.5.0] - 2026-10-27

### Added

- Overlay toast notify on newly unlocked achievements: C# SDK, Godot, Unreal, and RPG Maker.

## [1.4.2] - 2026-10-27

### Fixed

- Fixed content builder login failing if password contained a "!"

## [1.4.1] - 2026-10-27

### Fixed

- Launch contract handshake accepts newer launcher contract versions at or above `MinCompatibleContractVersion`.

## [1.4.0] - 2026-10-27

### Added

- Player Stats client matching the game-session stats API
- Stats and OpenID helpers on Godot, Unreal, and RPG Maker bindings
- Rebuilt Unity `Plugins/Curated.Sdk.dll` for 1.4.0 (includes Stats)

## [1.3.0] - 2026-10-25

### Changed

- Content Builder: `--depot-id <app_id>_<NN>` replaces `--depot-key`

## [1.2.0] - 2026-10-25

### Added

- `GetCurrentGameLanguage` in every engine binding

## [1.0.1] - 2026-10-18

### Added

- Content Builder CLI tooling and Agents.md

## [1.0.0] - 2026-10-18

### Added

- Initial Curated.Sdk public API: launcher auth, entitlements, achievements, Cloud Save, friends and presence

[Unreleased]: https://github.com/CuratedGames/curated-sdk/compare/v1.5.0...HEAD
[1.5.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.2...v1.5.0
[1.4.2]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.0.1...v1.2.0
[1.0.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/CuratedGames/curated-sdk/releases/tag/v1.0.0
"""


class ClassifyTests(unittest.TestCase):
    def test_content_builder_excluded(self) -> None:
        self.assertFalse(
            classify_bullet(
                "- Content Builder depot uploads: adaptive chunk size\n", "unity"
            )
        )
        self.assertFalse(
            classify_bullet(
                "- Content Builder: `--depot-id` replaces `--depot-key`\n", "godot"
            )
        )

    def test_unity_specific_included_for_unity_only(self) -> None:
        bullet = "- Rebuilt Unity `Plugins/Curated.Sdk.dll` for 1.4.0\n"
        self.assertTrue(classify_bullet(bullet, "unity"))
        self.assertFalse(classify_bullet(bullet, "godot"))
        self.assertFalse(classify_bullet(bullet, "unreal"))
        self.assertFalse(classify_bullet(bullet, "rpgmaker"))

    def test_shared_sdk_included_everywhere(self) -> None:
        bullet = (
            "- Launch contract handshake accepts newer launcher contract versions\n"
        )
        for engine in ("unity", "godot", "unreal", "rpgmaker"):
            self.assertTrue(classify_bullet(bullet, engine), engine)

    def test_multi_engine_list_with_csharp_includes_unity(self) -> None:
        bullet = (
            "- Overlay toast notify: C# SDK, Godot, Unreal, and RPG Maker.\n"
        )
        self.assertTrue(classify_bullet(bullet, "unity"))
        self.assertTrue(classify_bullet(bullet, "godot"))
        self.assertTrue(classify_bullet(bullet, "unreal"))
        self.assertTrue(classify_bullet(bullet, "rpgmaker"))

    def test_other_engines_without_shared_skips_unity(self) -> None:
        # Hypothetical Godot-only note with no shared markers.
        bullet = "- Godot addon enables curated autoload on project load\n"
        self.assertFalse(classify_bullet(bullet, "unity"))
        self.assertTrue(classify_bullet(bullet, "godot"))


class FilterSectionTests(unittest.TestCase):
    def test_drops_empty_version(self) -> None:
        section = (
            "## [1.4.2] - 2026-10-27\n\n"
            "### Fixed\n\n"
            '- Fixed content builder login failing if password contained a "!"\n'
        )
        self.assertIsNone(filter_section(section, "unity"))

    def test_keeps_empty_unreleased(self) -> None:
        section = (
            "## [Unreleased]\n\n"
            "### Changed\n\n"
            "- Content Builder uploads depot files via file-level CAS.\n"
        )
        out = filter_section(section, "unity")
        assert out is not None
        self.assertEqual(out.strip(), "## [Unreleased]")


class RenderTests(unittest.TestCase):
    def test_unity_omits_tooling_keeps_shared(self) -> None:
        out = render_engine_changelog(ROOT, "unity")
        self.assertIn("# Changelog", out)
        self.assertIn("Keep a Changelog", out)
        self.assertIn("## [Unreleased]", out)
        self.assertNotIn("file-level CAS", out)
        self.assertNotIn("content builder login", out.lower())
        self.assertNotIn("`--depot-id`", out)
        self.assertNotIn("Content Builder CLI tooling", out)
        self.assertNotIn("## [1.4.2]", out)
        self.assertNotIn("## [1.3.0]", out)
        self.assertNotIn("## [1.0.1]", out)
        self.assertIn("## [1.5.0]", out)
        self.assertIn("Overlay toast notify", out)
        self.assertIn("## [1.4.1]", out)
        self.assertIn("Launch contract handshake", out)
        self.assertIn("## [1.4.0]", out)
        self.assertIn("Player Stats client", out)
        self.assertIn("Rebuilt Unity", out)
        self.assertIn("## [1.2.0]", out)
        self.assertIn("GetCurrentGameLanguage", out)
        self.assertIn("## [1.0.0]", out)
        self.assertIn(
            "[1.5.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.1...v1.5.0",
            out,
        )

    def test_godot_keeps_godot_helpers_skips_unity_dll_note(self) -> None:
        out = render_engine_changelog(ROOT, "godot")
        self.assertIn("Stats and OpenID helpers on Godot", out)
        self.assertNotIn("Rebuilt Unity", out)
        self.assertIn("Overlay toast notify", out)

    def test_rpgmaker_matches_rpg_maker_token(self) -> None:
        out = render_engine_changelog(ROOT, "rpgmaker")
        self.assertIn("RPG Maker", out)
        self.assertNotIn("Rebuilt Unity", out)


if __name__ == "__main__":
    unittest.main()
