# Achievements and Stats

**Game Build > Achievements and Stats** defines what the SDK can unlock and write. See
[Achievements and stats](/guide/achievements-stats) for the game side.

## Import from Steam

If the app is linked to a Steam listing with stats and achievements, **Import from Steam** copies
them, including icons. Stats are imported first so progress achievements link correctly.
**Clear and reimport from Steam** replaces what you have.

## Stats

Under **Stats Configuration**, click **New Stat** and set:

- **API Name**: the name the game uses.
- **Display Name**.
- **Type**: INT or FLOAT.
- **Set By**: **Client** (the SDK may write it) or **Server** (only your game server).
- **Increment Only?**, **Max Change**, **Min Value**, **Max Value**, **Window**,
  **Default Value**, **Aggregated**.

## Achievements

Under **New Achievement**, set the **API Name** (letters, numbers, underscore), **Display
name**, and **Description**, then **Save achievement**. Defined achievements are listed below.
Owners of the game can unlock them in-game.
