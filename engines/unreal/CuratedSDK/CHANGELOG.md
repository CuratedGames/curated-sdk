# Changelog

All notable changes to the Curated SDK Unreal plugin are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Generated from the repository root `CHANGELOG.md`: engine-specific and shared SDK
entries are kept; Content Builder and other tooling-only notes are omitted unless
they affect this package.

## [Unreleased]

## [1.5.0] - 2026-10-27

### Added

- Overlay toast notify on newly unlocked achievements (`CURATED_OVERLAY_ENDPOINT` / `CURATED_OVERLAY_TOKEN`): C# SDK, Godot, Unreal, and RPG Maker. Unlock API toast fields (`display_name`, `description`, `icon_achieved_uri`, `global_unlock_percent`).

## [1.4.1] - 2026-10-27

### Fixed

- Launch contract handshake accepts newer launcher contract versions at or above `MinCompatibleContractVersion` instead of rejecting anything above the SDK's known max. Shipped games keep working after launcher upgrades when their used features remain available. `unsupported_contract_version` now means below-minimum or invalid only.

## [1.4.0] - 2026-10-27

### Added

- Player Stats client (`Stats.GetStat` / `SetStat` / `StoreStatsAsync`, plus immediate set/increment) matching the game-session stats API ([#10](https://github.com/CuratedGames/curated-sdk/pull/10))
- `CuratedClient.InitAsync` and `CuratedClient.IsEntitled`; every Init path verifies session ownership
- Stats and OpenID helpers on Godot, Unreal, and RPG Maker bindings (fail closed until `init_from_launcher` / `InitFromLauncher` succeeds)
- Unit tests for the entitlement fail-closed gate

### Changed

- `CuratedClient.Init(appId, token)` now verifies ownership (same as `InitFromLauncherAsync`) and leaves Curated shut down with `not_entitled` / `session_invalid` on failure (previously it only stored the token)
- Game-scoped APIs (achievements, stats, Cloud Save, friends, presence, OpenID) require a successful ownership check

## [1.2.0] - 2026-10-25

### Added

- `GetCurrentGameLanguage` (and Steam API name helper) in every engine binding ([#4](https://github.com/CuratedGames/curated-sdk/pull/4))

### Changed

- Copyright and trademark notice: Margherita Roncone, licensed to Fatum Games SL ([#3](https://github.com/CuratedGames/curated-sdk/pull/3))

## [1.1.0] - 2026-10-22

### Added

- Multi-engine SDK: launch contract v1 and Unity, Godot, Unreal, RPG Maker bindings ([#2](https://github.com/CuratedGames/curated-sdk/pull/2))

### Changed

- Docs: remove self-referential GitHub URL mentions ([#1](https://github.com/CuratedGames/curated-sdk/pull/1))

## [1.0.0] - 2026-10-18

### Added

- Initial Curated.Sdk public API: launcher auth, entitlements, achievements, Cloud Save, friends and presence

[Unreleased]: https://github.com/CuratedGames/curated-sdk/compare/v1.5.0...HEAD
[1.5.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.1...v1.5.0
[1.4.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.2.0...v1.4.0
[1.2.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/CuratedGames/curated-sdk/releases/tag/v1.0.0
