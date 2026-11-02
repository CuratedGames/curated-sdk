# Curated SDK

Integrate games with [Curated](https://curated.games). When the Curated launcher starts a game, the SDK signs the player in, confirms they own the game, and exposes achievements, stats, Cloud Save, friends, presence, and in-game purchases. It includes bindings for Unity, .NET, Godot 4, Unreal Engine 5, and RPG Maker MZ / MV, plus the Content Builder CLI for depot uploads.

Guides, install steps, and the API reference are at **[docs.curated.games](https://docs.curated.games)**.

This repository is source-available under the [Curated SDK License](LICENSE.md). It is not an open-source license. See [License](#license).

## Documentation

| Topic | Page |
| --- | --- |
| Install and what every binding does | [SDK overview](https://docs.curated.games/guide/) |
| Unity | [Unity](https://docs.curated.games/guide/unity) |
| .NET (C#) | [.NET](https://docs.curated.games/guide/dotnet) |
| Godot 4 | [Godot 4](https://docs.curated.games/guide/godot) |
| Unreal Engine 5 | [Unreal Engine 5](https://docs.curated.games/guide/unreal) |
| RPG Maker MZ / MV | [RPG Maker](https://docs.curated.games/guide/rpgmaker) |
| Launch contract and error codes | [Launch contract](https://docs.curated.games/guide/launch-contract) |
| Testing outside the launcher | [Testing outside the launcher](https://docs.curated.games/guide/testing) |
| Dedicated server builds | [Unity](https://docs.curated.games/guide/unity#dedicated-server-and-webgl), [Godot 4](https://docs.curated.games/guide/godot#dedicated-servers), [Unreal Engine 5](https://docs.curated.games/guide/unreal#install) |
| Achievements and stats | [Achievements and stats](https://docs.curated.games/guide/achievements-stats) |
| Game language | [Game language](https://docs.curated.games/guide/game-language) |
| Cloud Save, friends, and presence | [Cloud Save, friends, presence](https://docs.curated.games/guide/cloud-friends-presence) |
| In-game purchases | [In-game purchases](https://docs.curated.games/guide/purchases) |
| OpenID Connect | [OpenID Connect](https://docs.curated.games/guide/openid) |
| Migrating from Steam | [Migrating from Steam](https://docs.curated.games/guide/migrating-from-steam) |
| Content Builder | [Content Builder](https://docs.curated.games/guide/content-builder) |
| API reference | [Overview](https://docs.curated.games/reference/) |
| .NET API | [.NET reference](https://docs.curated.games/reference/dotnet) |
| Unity API | [Unity reference](https://docs.curated.games/reference/unity) |
| Godot API | [Godot reference](https://docs.curated.games/reference/godot) |
| Unreal API | [Unreal reference](https://docs.curated.games/reference/unreal) |
| RPG Maker API | [RPG Maker reference](https://docs.curated.games/reference/rpgmaker) |
| Content Builder CLI | [CLI reference](https://docs.curated.games/reference/content-builder) |
| Language codes | [Game languages](https://docs.curated.games/reference/languages) |
| Release notes | [Changelog](https://docs.curated.games/changelog) |

## Developing this package

```bash
dotnet test Curated.Sdk.sln --configuration Release
node --test engines/rpgmaker/CuratedSDK.test.mjs
python3 scripts/check-game-languages.py
python3 scripts/check-public-api.py
python3 scripts/check-docs-versions.py
python3 -m unittest discover -s scripts -p 'test_*.py'
bash scripts/pack-unity-zip.sh artifacts/sdk-unity
bash scripts/pack-engines.sh artifacts/sdk-unity/publish/Curated.Sdk.dll artifacts/engines
cd tooling/content-builder && cargo test && cargo build --release
```

Godot and Unreal tests run inside the engine. See [Godot 4](https://docs.curated.games/guide/godot#tests) and [Unreal Engine 5](https://docs.curated.games/guide/unreal#tests).

### Documentation site

[docs.curated.games](https://docs.curated.games) is VitePress built from [`docs/`](docs). It shows the latest release only and is deployed by [`.github/workflows/docs.yml`](.github/workflows/docs.yml) on every push to `main` and after each release.

```bash
cd docs && npm install && npm run dev   # http://localhost:5173
```

`npm run build` runs `scripts/docs_gen.py` first. That script writes the pages nobody edits by hand (they are gitignored): the API reference (from `contracts/public-api.v1.json`), the Content Builder CLI reference (parsed from `tooling/content-builder/src`), the language table (from the fixture), the changelog (from GitHub Releases, or `CHANGELOG.md` offline), and the SDK version.

Write `%SDK_VERSION%` / `%SDK_TAG%` instead of a version number. Import code from files CI runs (`<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#region-name`) instead of pasting it. Embed videos with `<VideoEmbed src="https://www.youtube.com/watch?v=..." title="..." />` (YouTube or an MP4/WebM URL; never commit video files).

#### What fails CI when docs drift

| Check | Fails when | Fix |
|-------|------------|-----|
| `scripts/check-public-api.py` (`engines`, `docs` jobs) | A binding has a public member missing from `contracts/public-api.v1.json`, the manifest lists a member that no longer exists, or a description is empty | `python3 scripts/check-public-api.py --write`, then replace each `TODO` |
| `scripts/check-docs-updated.py` (`Docs required` workflow) | A PR changes a public API signature or a Content Builder command/flag/script field but touches neither `docs/` nor the manifest | Update the docs, or add the `docs-not-needed` label |
| `scripts/check-docs-versions.py` (`docs` job) | An authored docs page contains a released version or a `vX.Y.Z` tag | Use `%SDK_VERSION%` / `%SDK_TAG%` |
| `npm run build` (`docs` job) | A link points at a missing page | Fix the link |
| `scripts/check-docs-snippets.py` (`docs` job) | An imported snippet file or `#region` is gone, or the built site still has a `%SDK_VERSION%` token | Fix the import or restore the region |
| `dotnet test`, `node --test`, `cargo test` | A docs snippet no longer compiles or runs | Fix the code in the test file; the page updates itself |
| `check-admin-docs.mjs` in CuratedGames/curated | A **bold** UI label in `docs/admin` is gone from the admin app, or an admin route has no docs page | Update `docs/admin` here (merge first), then the route map there |
| `check-docs-brand.mjs` in CuratedGames/curated | A store color token, font, or brand file differs from `docs/.vitepress/theme/brand.css` or `docs/public/brand` | In curated run `node scripts/check-docs-brand.mjs --docs ../curated-sdk/docs --write`, then open a PR here |

Godot and Unreal snippets are not compiled in CI (no engine install). Their public functions are still checked by the manifest, and their language tables by `check-game-languages.py`.

### Changelog

Releases follow [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) in [CHANGELOG.md](CHANGELOG.md). Published notes are on the [changelog](https://docs.curated.games/changelog).

When you bump `<Version>` in `Curated.Sdk.csproj` (and the matching Unity `package.json`) and merge to `main`, the Release workflow:

1. Promotes any bullets under `[Unreleased]`, or if that section is empty, generates entries from commits since the previous `v*` tag with [git-cliff](https://github.com/orhun/git-cliff) (`cliff.toml`).
2. Writes the version section into `CHANGELOG.md` and attaches those notes to the GitHub Release for `v<version>`.
3. Regenerates each engine package `CHANGELOG.md` (Unity, Godot, Unreal, RPG Maker) via `scripts/engine_changelogs.py`, filtering to engine-specific and shared SDK notes so Unity Package Manager can show version history. Content Builder / tooling-only bullets are omitted unless they affect that package.

**How to add notes for the next release**

- Preferred for features and fixes: put a Keep a Changelog bullet under `## [Unreleased]` in `CHANGELOG.md` (use `### Added` / `### Changed` / `### Fixed` / …).
- Or name the PR / commit with a prefix the cliff parsers understand: `[Feature]`, `[Bugfix]`, `[Docs]`, `[Chore]`, or conventional `feat:` / `fix:` / `docs:` / `chore:`.
- Do not hand-edit `engines/**/CHANGELOG.md`; CI checks they match the filter. Regenerate with `python3 scripts/engine_changelogs.py --write`.

Do not hand-edit past released sections unless you are correcting a mistake. Version tags stay `vMAJOR.MINOR.PATCH`.

## License

See [LICENSE.md](LICENSE.md). Ship the official packages (DLL, NuGet `.nupkg` from Releases, and the engine bindings in `engines/`) only as part of your game. Never log or store the player's session token, and send it only to the Curated API address the launcher provides.

Copyright (c) Margherita Roncone, licensed to Fatum Games SL (Business ID: B21832985), Calle Mediodia 7, 35015, Las Palmas De Gran Canaria, Las Palmas, Spain. Curated and Curated Games are trademarks of Margherita Roncone, licensed to Fatum Games SL. Support: support@curated.games.
