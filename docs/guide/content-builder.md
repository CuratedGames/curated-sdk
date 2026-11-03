# Content Builder

`curated-content-builder` uploads your game files into a depot of an app you own, including
drafts and titles still in review. After the upload, you set the build live in the admin.

## Download

Take the binary from [Releases](https://github.com/CuratedGames/curated-sdk/releases/tag/%SDK_TAG%):
`curated-content-builder-windows-x64.exe` or `curated-content-builder-linux-x64`. Or build it
(Rust 1.89+):

```bat
cd tooling\content-builder
cargo build --release
```

## 1. Sign in

```bat
curated-content-builder.exe login --login you@studio.com
```

Omit `--password` to type it at a prompt. In bash, `!` inside double quotes is history expansion:
single-quote a password that contains `!`, or use the prompt. The session is saved to
`%USERPROFILE%\.curated\credentials.json` (`~/.curated/credentials.json` elsewhere). Check it with
`whoami`, remove it with `logout`.

## 2. Find the depot ID

Every depot has an ID `<app_id>_<NN>`. `<app_id>_00` is the base depot.

```bat
curated-content-builder.exe list-depots --app-id 1000
```

The same IDs are in the admin under [Game Build > Depots](/admin/game-build/depots).

## 3. Upload

```bat
curated-content-builder.exe upload ^
  --app-id 1000 ^
  --depot-id 1000_00 ^
  --content C:\game\content ^
  --version 1.2.3
```

Without `--depot-id`, the build goes to the base depot `<app_id>_00`.

What happens:

1. Checks the depot ID and that you own the app.
2. Uploads only the files Curated does not have yet. Override chunk size and parallel uploads
   with `--chunk-size` and `--concurrency`.
3. Registers the build on the depot. It does not set it live.

## 4. Set the build live

Open [Game Build > Builds](/admin/game-build/builds) in the admin, pick **Default** or **Beta**,
and set the build live on that channel.

## Build scripts

Put the settings in a TOML file and run `upload --script <file>`. Paths are relative to the
script.

<<< @/../tooling/content-builder/examples/simple_app_build.toml#script{toml}

## Environment variables

| Variable | Purpose |
| --- | --- |
| `CURATED_API_BASE` | API origin (default `https://api.curated.games`) |
| `CURATED_TOKEN` | Session token (skips the credentials file) |
| `CURATED_DEPOT_ID` | Default `--depot-id` for `upload` |
| `CURATED_LOGIN`, `CURATED_EMAIL`, `CURATED_USERNAME` | Login identity |
| `CURATED_PASSWORD` | Login password |
| `CURATED_CREDENTIALS_PATH` | Alternate credentials file |

All flags: [Content Builder CLI reference](/reference/content-builder).
