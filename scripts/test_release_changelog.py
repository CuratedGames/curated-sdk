#!/usr/bin/env python3
"""Unit tests for scripts/release_changelog.py."""

from __future__ import annotations

import unittest

from release_changelog import (
    build_release_notes,
    ensure_changelog_for_version,
    extract_section,
    insert_version_section,
    promote_unreleased,
    rebuild_compare_links,
    unreleased_is_empty,
)

SAMPLE = """# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.3.1] - 2026-10-27

### Fixed

- Unity Git install path for `com.curatedgames.sdk` ([#9](https://github.com/CuratedGames/curated-sdk/pull/9))

## [1.3.0] - 2026-10-25

### Changed

- Content Builder uses `--depot-id` instead of `--depot-key` ([#5](https://github.com/CuratedGames/curated-sdk/pull/5))

[Unreleased]: https://github.com/CuratedGames/curated-sdk/compare/v1.3.1...HEAD
[1.3.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/CuratedGames/curated-sdk/releases/tag/v1.3.0
"""


class ExtractTests(unittest.TestCase):
    def test_extract_existing_version(self) -> None:
        section = extract_section(SAMPLE, "1.3.1")
        assert section is not None
        self.assertIn("## [1.3.1] - 2026-10-27", section)
        self.assertIn("Unity Git install path", section)
        self.assertNotIn("## [1.3.0]", section)

    def test_extract_missing_version(self) -> None:
        self.assertIsNone(extract_section(SAMPLE, "9.9.9"))


class UnreleasedTests(unittest.TestCase):
    def test_empty_unreleased(self) -> None:
        self.assertTrue(unreleased_is_empty("## [Unreleased]\n\n"))
        self.assertTrue(
            unreleased_is_empty("## [Unreleased]\n\n### Added\n\n")
        )

    def test_non_empty_unreleased(self) -> None:
        self.assertFalse(
            unreleased_is_empty("## [Unreleased]\n\n### Added\n\n- New API\n")
        )

    def test_promote_unreleased(self) -> None:
        src = "## [Unreleased]\n\n### Added\n\n- New API\n"
        out = promote_unreleased(src, "1.4.0", "2026-11-01")
        self.assertTrue(out.startswith("## [1.4.0] - 2026-11-01\n"))
        self.assertIn("- New API", out)


class InsertTests(unittest.TestCase):
    def test_insert_new_version_clears_unreleased(self) -> None:
        text = SAMPLE.replace(
            "## [Unreleased]\n\n",
            "## [Unreleased]\n\n### Added\n\n- Something pending\n\n",
        )
        section = "## [1.4.0] - 2026-11-01\n\n### Added\n\n- Something pending\n"
        updated = insert_version_section(text, section, "1.4.0")
        self.assertIn("## [Unreleased]\n\n## [1.4.0] - 2026-11-01", updated)
        self.assertIn("[Unreleased]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.0...HEAD", updated)
        self.assertIn(
            "[1.4.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.3.1...v1.4.0",
            updated,
        )
        # Pending note should not remain under Unreleased.
        unreleased = extract_section(updated, "Unreleased")
        assert unreleased is not None
        self.assertTrue(unreleased_is_empty(unreleased))

    def test_rebuild_compare_links_order(self) -> None:
        links = rebuild_compare_links(["Unreleased", "1.2.0", "1.1.0", "1.0.0"])
        self.assertIn(
            "[Unreleased]: https://github.com/CuratedGames/curated-sdk/compare/v1.2.0...HEAD",
            links,
        )
        self.assertIn(
            "[1.0.0]: https://github.com/CuratedGames/curated-sdk/releases/tag/v1.0.0",
            links,
        )


class EnsureTests(unittest.TestCase):
    def test_idempotent_when_version_exists(self) -> None:
        updated, section, changed = ensure_changelog_for_version(SAMPLE, "1.3.1")
        self.assertFalse(changed)
        self.assertEqual(updated, SAMPLE)
        self.assertIn("Unity Git install path", section)

    def test_promotes_unreleased_when_present(self) -> None:
        text = SAMPLE.replace(
            "## [Unreleased]\n\n",
            "## [Unreleased]\n\n### Added\n\n- Player stats client\n\n",
        )
        updated, section, changed = ensure_changelog_for_version(
            text,
            "1.4.0",
            today="2026-11-02",
            cliff_section="## [1.4.0] - 2026-11-02\n\n### Changed\n\n- should not use\n",
        )
        self.assertTrue(changed)
        self.assertIn("Player stats client", section)
        self.assertNotIn("should not use", section)
        self.assertIsNotNone(extract_section(updated, "1.4.0"))

    def test_uses_cliff_when_unreleased_empty(self) -> None:
        cliff = "## [1.4.0] - 2026-11-03\n\n### Added\n\n- Auto from commits\n"
        updated, section, changed = ensure_changelog_for_version(
            SAMPLE,
            "1.4.0",
            today="2026-11-03",
            cliff_section=cliff,
        )
        self.assertTrue(changed)
        self.assertIn("Auto from commits", section)
        self.assertIn("## [1.4.0] - 2026-11-03", updated)


class NotesTests(unittest.TestCase):
    def test_build_release_notes_includes_install(self) -> None:
        section = extract_section(SAMPLE, "1.3.1")
        assert section is not None
        notes = build_release_notes(section, "1.3.1")
        self.assertIn("**Curated.Sdk** 1.3.1", notes)
        self.assertIn("Unity Git install path", notes)
        self.assertIn("com.curatedgames.sdk", notes)
        self.assertIn("#v1.3.1", notes)
        self.assertNotIn("## [1.3.1]", notes)


if __name__ == "__main__":
    unittest.main()
