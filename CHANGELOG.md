# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.4.0] - 2026-10-27

### Added

- Player Stats client (`Stats.GetStat` / `SetStat` / `StoreStatsAsync`, plus immediate set/increment) matching the game-session stats API ([#10](https://github.com/CuratedGames/curated-sdk/pull/10))
- `CuratedClient.InitAsync` and `CuratedClient.IsEntitled`; every Init path verifies session ownership
- Stats and OpenID helpers on Godot, Unreal, and RPG Maker bindings (fail closed until `init_from_launcher` / `InitFromLauncher` succeeds)
- Unit tests for the entitlement fail-closed gate

### Changed

- `CuratedClient.Init(appId, token)` now verifies ownership (same as `InitFromLauncherAsync`) and leaves Curated shut down with `not_entitled` / `session_invalid` on failure (previously it only stored the token)
- Game-scoped APIs (achievements, stats, Cloud Save, friends, presence, OpenID) require a successful ownership check
- Rebuilt Unity `Plugins/Curated.Sdk.dll` for 1.4.0 (includes Stats)

## [1.3.1] - 2026-10-27

### Added

- OpenID Connect ID token minting for Unity Authentication (`GetOpenIdIdTokenAsync`) ([#6](https://github.com/CuratedGames/curated-sdk/pull/6))
- `curated_id` on sessions; Unity Editor can use a running launcher session ([#7](https://github.com/CuratedGames/curated-sdk/pull/7))

### Changed

- Public-facing docs and Unity package id rename to `com.curatedgames.sdk`; Git URL install is the recommended Unity path ([#8](https://github.com/CuratedGames/curated-sdk/pull/8))

### Fixed

- Unity Package Manager Git install for `com.curatedgames.sdk` at tag `v1.3.1` (path rename from `games.curated.sdk`) ([#9](https://github.com/CuratedGames/curated-sdk/pull/9))

## [1.3.0] - 2026-10-25

### Changed

- Content Builder: `--depot-id <app_id>_<NN>` replaces `--depot-key` ([#5](https://github.com/CuratedGames/curated-sdk/pull/5))

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

## [1.0.1] - 2026-10-18

### Added

- Content Builder CLI tooling and Agents.md

## [1.0.0] - 2026-10-18

### Added

- Curated.Sdk 1.0.0 public API
