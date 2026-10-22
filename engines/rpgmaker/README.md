# Curated SDK for RPG Maker MZ / MV

Single-file plugin implementing launch contract v1 (`contracts/launch-contract.md`).

## Install

1. Copy `CuratedSDK.js` into the project's `js/plugins/` folder.
2. **Plugin Manager**: add **CuratedSDK**, set **Curated app id**.
3. Optional: **Quit when not owned** (default on) and **Blocked message**, shown before quitting.

The title screen waits until the ownership check finishes. Test play from the editor skips
the check.

## Achievements

- Event command **Plugin Command > CuratedSDK > Unlock achievement** (MZ), with the
  achievement's API name.
- Script call: `Curated.unlockAchievement("BOSS_1");`

`Curated.session` holds `userId`, `username`, `language`, and `entitlement` once the check
succeeds. `Curated.checkEntitlement()` re-checks ownership.

## Tests

```bash
node --test engines/rpgmaker/CuratedSDK.test.mjs
```
