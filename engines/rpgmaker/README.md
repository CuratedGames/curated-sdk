# Curated SDK for RPG Maker MZ / MV

Single-file plugin implementing launch contract v1 (`contracts/launch-contract.md`).

## Install

1. Copy `CuratedSDK.js` from this repository's
   [Releases](https://github.com/CuratedGames/curated-sdk/releases) (or from `engines/rpgmaker/`)
   into the project's `js/plugins/` folder.
2. **Plugin Manager**: add **CuratedSDK**, set **Curated app id**.
3. Optional: **Quit when not owned** (default on) and **Blocked message**, shown before quitting.

The title screen waits until the ownership check finishes. Test play from the editor skips
the check.

## Achievements

- Event command **Plugin Command > CuratedSDK > Unlock achievement** (MZ), with the
  achievement's API name.
- Script call: `Curated.unlockAchievement("BOSS_1");`

`Curated.session` holds `userId`, `curatedId`, `username`, `language` (raw launcher value),
`gameLanguage`, and `entitlement` once the check succeeds. `Curated.checkEntitlement()`
re-checks ownership.

## Game language

`Curated.getCurrentGameLanguage()` returns the language the player selected in the Curated
launcher (`"en"`, `"es"`, `"it"`, ...), the language the launcher downloads depots for, or `"en"`
outside the launcher. It works before the ownership check finishes.

If you are porting from Steam, see [Migrating from Steam](#migrating-from-steam).

## Migrating from Steam

`Curated.getCurrentGameLanguageSteamApiName()` returns Steam client language names
(`"english"`, `"italian"`, ...) for code that already expects those strings.

## Tests

```bash
node --test engines/rpgmaker/CuratedSDK.test.mjs
```
