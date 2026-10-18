# Curated Content Builder

Windows-first CLI to upload game files into a **depot** for any app you own, including drafts and titles still in review. Sign in once, list depots, pack a content folder, upload, and register the build.

Canonical home: [RikuTheFuffs/curated-sdk](https://github.com/RikuTheFuffs/curated-sdk) under `tooling/content-builder`.

Codesign of this CLI is deferred; unsigned builds are fine for developers.

## Requirements

| Tool | Notes |
|------|--------|
| Rust 1.89+ | `rustup` / `rust-toolchain.toml` in this folder |
| Curated developer account | Org owner of the app |

## Build (Windows)

```bat
cd tooling\content-builder
cargo build --release
```

Binary: `target\release\curated-content-builder.exe`

Prebuilt Windows binaries ship on [curated-sdk Releases](https://github.com/RikuTheFuffs/curated-sdk/releases) when CI publishes them.

Cross-compile from Linux/macOS (optional):

```bash
cd tooling/content-builder
cargo build --release --target x86_64-pc-windows-gnu
```

## Sign in

Email or username + password. The session token is cached locally and reused by later commands.

```bat
curated-content-builder.exe login --login you@studio.com --password "…" --api-base https://api.curated.games
curated-content-builder.exe login --username studio_dev --password "…"
```

`--email` is accepted as an alias of `--login`.

Session file: `%USERPROFILE%\.curated\credentials.json` (or `~/.curated/credentials.json`). Override with `CURATED_CREDENTIALS_PATH`, or pass `--token` / `CURATED_TOKEN` per command.

```bat
curated-content-builder.exe whoami
curated-content-builder.exe logout
```

## List depots

Only apps you own are allowed (checked via the API before listing).

```bat
curated-content-builder.exe list-depots --app-id 1000
```

## Upload a content folder

Uploads fail unless the signed-in developer owns the app.

```bat
curated-content-builder.exe upload ^
  --app-id 1000 ^
  --depot-key content ^
  --content C:\game\content ^
  --version 1.2.3 ^
  --desc "Weekly Windows build" ^
  --build-output C:\game\output
```

What happens:

1. Confirms ownership of the app.
2. Packs `content` into a zip under `build-output` (default `depot.zip`).
3. Requests a chunked upload slot for that app + depot.
4. Uploads ~1 MiB chunks (override with `--chunk-size`).
5. Registers the build against the depot (does **not** set Default/Beta live; use admin for that).

## Build script

```bat
curated-content-builder.exe upload --script scripts\simple_app_build.toml
```

See [`examples/simple_app_build.toml`](examples/simple_app_build.toml). Relative paths resolve from the script file.

## Environment

| Variable | Purpose |
|----------|---------|
| `CURATED_API_BASE` | API origin (default `https://api.curated.games`) |
| `CURATED_TOKEN` | Session token (skips credentials file) |
| `CURATED_LOGIN` / `CURATED_EMAIL` / `CURATED_USERNAME` | Login identity |
| `CURATED_PASSWORD` | Login password |
| `CURATED_CREDENTIALS_PATH` | Alternate credentials file |

## Related admin UI

After upload, open **Game Build** on admin to mark the build clean and set Default or Beta live. Depots and packages are managed under **Packages**.

See also [Agents.md](../../Agents.md) at the repo root.
