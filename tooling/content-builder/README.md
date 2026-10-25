# Curated Content Builder

Windows-first CLI to upload game files into a **depot** for any app you own, including drafts and titles still in review. Sign in once, list depots, pack a content folder, upload, and register the build.

Lives in `tooling/content-builder` in this repository.

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

Prebuilt Windows binaries ship on this repository's Releases when CI publishes them.

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
  --depot-id 1000_00 ^
  --content C:\game\content ^
  --version 1.2.3 ^
  --desc "Weekly Windows build" ^
  --build-output C:\game\output
```

`--depot-id` is the depot ID `<app_id>_<NN>`: the app ID and the depot's two-digit number, for
example `1000_00` (the base depot) or `1000_01`. `list-depots` and admin **Game Build > Builds and
Depots** show them. Without `--depot-id` (or `depot_id` in the script) the build goes to
`<app_id>_00`. The builder checks the format before packing, so `--depot-id content` or a UUID fails
right away with the expected format; `--depot-key` and `depot_key` no longer exist.

What happens:

1. Validates the depot ID and confirms ownership of the app.
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
| `CURATED_DEPOT_ID` | Default `--depot-id` for `upload` (`<app_id>_<NN>`) |
| `CURATED_LOGIN` / `CURATED_EMAIL` / `CURATED_USERNAME` | Login identity |
| `CURATED_PASSWORD` | Login password |
| `CURATED_CREDENTIALS_PATH` | Alternate credentials file |

## Related admin UI

After upload, open **Game Build** on admin to mark the build clean and set Default or Beta live. Depots are managed under **Game Build > Builds and Depots**, packages under **Packages and product keys**.

See also [Agents.md](../../Agents.md) at the repo root.
