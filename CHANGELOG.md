# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Unity: `OverlayToStoreFlag` is declared in `Curated.Unity`. `CuratedUnity.OpenStoreOverlay` no longer requires a reference to `Curated.Sdk.dll`, so assemblies that set Override References can call it. Values match `Curated.Sdk.OverlayToStoreFlag`.

## [1.10.0] - 2026-11-05

### Changed

- C# SDK: `OpenStoreOverlay` moved from `Friends` to `CuratedClient.OpenStoreOverlay(long appId, OverlayToStoreFlag flag)`. Unity calls `CuratedUnity.OpenStoreOverlay(appId, flag)`. `OverlayToStoreFlag` is unchanged (`None` = 0, `AddToCart` = 1, `AddToCartAndShow` = 2). The overlay line and the launcher error text are unchanged.
- Rebuilt Unity `Plugins/Curated.Sdk.dll` for the store overlay move.

## [1.9.0] - 2026-11-04

### Added

- C# SDK: `Friends.OpenStoreOverlay(long appId, OverlayToStoreFlag flag)` opens a published Curated store page in the launcher overlay (`CURATED_OVERLAY_ENDPOINT` / `CURATED_OVERLAY_TOKEN`). The Unity Editor attach path uses the same overlay channel. `OverlayToStoreFlag.None` opens the page with no cart action. `AddToCart` and `AddToCartAndShow` add a released or preorderable app to the cart, and `AddToCartAndShow` then shows the cart. The launcher answers on the same connection. Failures throw `CuratedException` with the launcher's English message.
- Rebuilt Unity `Plugins/Curated.Sdk.dll` for the store overlay.

## [1.8.0] - 2026-11-03
### Changed

- In-game purchase confirmation no longer takes a decision from the game. Unity `MicroTxn.AuthorizeAsync`, Godot `authorize_txn`, Unreal `AuthorizeTxn`, and RPG Maker `authorizeTxn` take the order id and transaction id, and the launcher overlay collects the decision. InitTxn no longer reads `language` or `format`.

### Fixed

- Unity 6 has no internal editor web view, so the Game view overlay is the launcher's own window placed on the Game view rectangle (`editor_frame`). Leaving play mode or quitting the game sends `game_closed`. The launcher cancels the microtransaction that game started and drops the editor session.

## [1.7.3] - 2026-11-03

### Fixed

- Unity Editor play shows the launcher overlay page over the Game view for purchases and achievements. The page is hosted in its own editor panel that tracks the Game view. The previous host attached Unity's internal web view under the Game view window, which did not paint, and it forced a transparent background that left the page blank. Player builds still do not send `editor_attach` and still require the launcher process (`CURATED_LAUNCHER` and the overlay variables).

## [1.7.2] - 2026-11-03

### Fixed

- Unity package `com.curatedgames.sdk` imports again: `Editor.meta`, `Editor/Curated.Unity.Editor.asmdef.meta`, and `Editor/CuratedGameViewPanel.cs.meta` had 34-character GUIDs, so Unity ignored the Editor assembly and the Game view panel ("does not have a valid GUID"). They and `Runtime/CuratedEditorOverlay.cs.meta` now have valid random GUIDs.

### Changed

- CI checks every Unity `.meta` under `engines/unity` (`scripts/check-unity-meta.py`): 32 lowercase hex GUIDs, no duplicates, no asset without a `.meta`, and no orphan `.meta`.

## [1.7.1] - 2026-11-03

### Added

- Unity Editor play sends `editor_attach` when `CuratedUnity` initializes, with the account signed in on the launcher, without `CURATED_LAUNCHER` and without the launcher having started Unity. On success the Game view loads the launcher overlay page (`overlay_url`), which is the purchase and achievement UI, and init does not return `not_launched_by_curated`. A launcher that is not signed in replies `not_signed_in`.
- Rebuilt Unity `Plugins/Curated.Sdk.dll` so Editor play can attach to the launcher overlay.

### Changed

- Document that InitTxn accepts one listed currency and converts it into the player's wallet currency. The launcher overlay confirms that converted reserve.

## [1.7.0] - 2026-11-02

### Changed

- In-game purchases in every engine binding (`MicroTxn.AuthorizeAsync`, Godot `authorize_txn`, Unreal `AuthorizeTxn`, and RPG Maker `authorizeTxn`) ask the Curated launcher overlay (`CURATED_OVERLAY_ENDPOINT` / `CURATED_OVERLAY_TOKEN`) and map `microtxn_purchase_result` status to authorized, denied, or error. The client's `authorized` argument is ignored. Outside the launcher the call returns error and does not POST the player's boolean. The launcher calls AuthorizeTxn on the live or sandbox path.
- Rebuilt Unity `Plugins/Curated.Sdk.dll` for the overlay purchase handoff.

## [1.6.0] - 2026-11-02

### Added

- Player in-game purchase authorization in every engine binding (`MicroTxn.AuthorizeAsync`): begin authorization for an `orderid` / `transid` the game server created, and report authorized, denied, or error. The client calls `POST /ICuratedMicroTxn/AuthorizeTxn/v1/` with the player session. The publisher key is never sent.
- Rebuilt Unity `Plugins/Curated.Sdk.dll` so the package includes in-game purchase authorization.

## [1.5.2] - 2026-10-30

### Changed

- Content Builder uploads depot files via file-level CAS (zstd or identity blobs) instead of packing a zip. `register_build` sends `delivery: cas_files` and per-file codec metadata. CLI flags are unchanged; staging lives under `build-output/.cas-staging` by default.

## [1.5.1] - 2026-10-27

### Changed

- Content Builder depot uploads: adaptive chunk size (up to 32 MiB) and parallel PUTs (up to 8) from a short bandwidth probe, respecting API limits. Overrides: `--chunk-size`, `--concurrency`. ([#18](https://github.com/CuratedGames/curated-sdk/pull/18))

## [1.5.0] - 2026-10-27

### Added

- Overlay toast notify on newly unlocked achievements (`CURATED_OVERLAY_ENDPOINT` / `CURATED_OVERLAY_TOKEN`): C# SDK, Godot, Unreal, and RPG Maker. Unlock API toast fields (`display_name`, `description`, `icon_achieved_uri`, `global_unlock_percent`).

## [1.4.2] - 2026-10-27

### Fixed

- Fixed content builder login failing if password contained a "!"

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

- Initial Curated.Sdk public API: launcher auth, entitlements, achievements, Cloud Save, friends and presence

[Unreleased]: https://github.com/CuratedGames/curated-sdk/compare/v1.10.0...HEAD
[1.10.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.9.0...v1.10.0
[1.9.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.8.0...v1.9.0
[1.8.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.7.3...v1.8.0
[1.7.3]: https://github.com/CuratedGames/curated-sdk/compare/v1.7.2...v1.7.3
[1.7.2]: https://github.com/CuratedGames/curated-sdk/compare/v1.7.1...v1.7.2
[1.7.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.7.0...v1.7.1
[1.7.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.6.0...v1.7.0
[1.6.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.5.2...v1.6.0
[1.5.2]: https://github.com/CuratedGames/curated-sdk/compare/v1.5.1...v1.5.2
[1.5.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.2...v1.5.0
[1.4.2]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.1...v1.4.2
[1.4.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.4.0...v1.4.1
[1.4.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.3.1...v1.4.0
[1.3.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/CuratedGames/curated-sdk/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/CuratedGames/curated-sdk/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/CuratedGames/curated-sdk/releases/tag/v1.0.0
