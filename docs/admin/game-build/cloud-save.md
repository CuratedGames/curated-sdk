# Cloud Save

**Game Build > Cloud Save** chooses which save files sync for players who own the game.

- Tick **Enable Cloud Save for this application**.
- **Save paths and patterns**: one per line, relative to the install folder or a known user
  folder, for example `saves/**/*.sav` or `%AppData%/Game/*`. Up to 64 lines.
  `%AppData%`, `%LocalAppData%`, `%Documents%`, and `%SavedGames%` are real folders
  on the player's machine. The launcher does not look for a directory named `%AppData%`.
- **Max size per player (MiB)**.

Click **Save Cloud Save settings**. The Curated launcher syncs these paths when the
player starts the game and when the game exits, including after a crash. The game
does not need to call the SDK. See
[Cloud Save, friends, presence](/guide/cloud-friends-presence).
