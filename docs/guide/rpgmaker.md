# RPG Maker MZ / MV

Single-file plugin, `CuratedSDK.js`.

## Install

1. Copy `CuratedSDK.js` from
   [Releases](https://github.com/CuratedGames/curated-sdk/releases/tag/%SDK_TAG%) into the
   project's `js/plugins/` folder.
2. **Plugin Manager**: add **CuratedSDK** and set **Curated app id**.
3. Optional: **Quit when not owned** (on by default) and **Blocked message**, shown before
   quitting.

The title screen waits until the ownership check finishes. Test play from the editor skips the
check.

## Achievements and stats

Event command (MZ): **Plugin Command > CuratedSDK > Unlock achievement**, with the
achievement's API name. MV: plugin command `Curated unlockAchievement BOSS_1`.

Script calls:

<<< @/../engines/rpgmaker/CuratedSDK.test.mjs#script-calls{js}

After the check succeeds, `Curated.session` holds the player:

<<< @/../engines/rpgmaker/CuratedSDK.test.mjs#session{js}

`Curated.checkEntitlement()` re-checks ownership. `Curated.getOpenIdIdToken()` mints an
[OpenID Connect](./openid) ID token. For purchases see [In-app purchases / Microtransactions](./purchases).

## Game language

`Curated.getCurrentGameLanguage()` reads the [game language](./game-language).

## Reference

[RPG Maker API reference](/reference/rpgmaker).
