# Cloud Save

**Game Build > Cloud Save** chooses which save files sync for players who own the game.

- Tick **Enable Cloud Save for this application**.
- **Save paths and patterns**: one per line, relative to the install folder or a known user
  folder, for example `saves/**/*.sav` or `%AppData%/Game/*`. Up to 64 lines.
- **Max size per player (MiB)**.

Click **Save Cloud Save settings**. The game syncs with the SDK: see
[Cloud Save, friends, presence](/guide/cloud-friends-presence).
