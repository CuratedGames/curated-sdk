# Maintaining this repo

Notes for maintainers and coding agents who change the SDK, the engine bindings, the Content
Builder or the docs site. Game integrators do not need this file: start with [Agents.md](Agents.md)
and [docs.curated.games](https://docs.curated.games). How to keep the docs in sync with code is in
[Agents.md, "Keeping docs.curated.games in sync"](Agents.md#keeping-docscuratedgames-in-sync-for-agents-changing-this-repo)
and the [README](README.md#developing-this-package); this file covers the rest.

This file is deliberately outside the Release workflow's `paths`, so editing it never starts a
release run.

## Workflow

- Branch from `main`, one pull request per change.
- Merge with a **merge commit** (not squash) whenever possible, so a change can be reverted as one
  commit. Head branches are deleted automatically after merging.
- `main` is protected. Required checks: `sdk`, `engines` and `content-builder` (jobs in
  [`.github/workflows/ci.yml`](.github/workflows/ci.yml)), and the branch must be up to date with
  `main`. The `changelog` and `docs` CI jobs and the `Docs required` workflow
  ([`docs-required.yml`](.github/workflows/docs-required.yml)) also run on every pull request
  but are not required today: still treat a red one as something to fix before merging.
- No em dashes anywhere: code, comments, docs, commit messages.

## Docs site (docs.curated.games)

- Built from [`docs/`](docs) (VitePress) and deployed by
  [`.github/workflows/docs.yml`](.github/workflows/docs.yml) on every push to `main`, after each
  completed "Release Curated.Sdk" run, and by manual dispatch.
- Deployed with GitHub Pages (Settings > Pages > Source: **GitHub Actions**). The custom domain
  comes from [`docs/public/CNAME`](docs/public/CNAME).
- `scripts/docs_gen.py` (run by `npm run build`) writes `docs/.vitepress/generated.json`,
  `docs/reference/*` and `docs/changelog.md`. They are gitignored (`docs/.gitignore`): never
  commit or hand-edit them; change their sources instead (the public API manifest, the Content
  Builder source, the language fixture, GitHub Releases or `CHANGELOG.md`).
- Deploy fails with "Not Found": GitHub Pages is not enabled for the repo, or its Source is not
  GitHub Actions. The deploy job uses the `github-pages` environment; if that environment has
  branch protection rules, `main` must be allowed.

## Game language

- `GetCurrentGameLanguage` (every binding) reads `CURATED_LANGUAGE`, which the launcher sets,
  normalizes it (trim, lowercase, `_` becomes `-`, then code, alias, Steam name, primary subtag)
  and returns a Curated code. A missing, blank or unknown value gives `en`; the Steam variant
  (`GetCurrentGameLanguageSteamApiName` and equivalents) then gives `english`.
- The language table lives in
  [`contracts/fixtures/game-language.v1.json`](contracts/fixtures/game-language.v1.json) (append
  only) and is copied into `Curated.Sdk/GameLanguages.cs`,
  `engines/godot/addons/curated/curated_contract.gd`, `engines/rpgmaker/CuratedSDK.js` and
  `engines/unreal/CuratedSDK/Source/CuratedSDK/Private/CuratedLaunchContract.cpp`.
- To change it, update the fixture first, then each table (and the Unreal automation test cases
  in `Private/Tests/CuratedLaunchContractTests.cpp` when the fixture cases change).
  `python3 scripts/check-game-languages.py` (CI job `engines`) fails on any drift.

## Godot and Unreal code is not compiled in CI

- CI has no Godot or Unreal install, so GDScript and C++ code, and the Godot and Unreal samples
  in the docs, are never compiled or run there.
- Their public functions are still checked against `contracts/public-api.v1.json` by
  `scripts/check-public-api.py`, and their language tables by `scripts/check-game-languages.py`.
- Test changes by hand in the engine: Godot with
  `godot --headless --script res://addons/curated/tests/run_game_language.gd -- <path to game-language.v1.json>`
  (and `run_conformance.gd` in the same folder), Unreal with the `Curated.*` automation tests
  (Session Frontend or `-ExecCmds="Automation RunTests Curated"`). Say in the pull request what
  you ran, or that you could not.

## Releases

- [`.github/workflows/release.yml`](.github/workflows/release.yml) runs on pushes to `main` that
  touch `Curated.Sdk/`, `Curated.Sdk.Tests/`, `tooling/content-builder/`, `scripts/`,
  `contracts/`, `engines/` (except the generated engine `CHANGELOG.md` files),
  `Curated.Sdk.sln`, `LICENSE.md`, `Agents.md` or `cliff.toml`, and by manual dispatch.
  `README.md`, `docs/`, this file and the workflow file itself do not trigger it.
- The release version is `<Version>` in `Curated.Sdk/Curated.Sdk.csproj`. If tag `v<version>`
  already exists, the run still builds and tests everything but leaves the tag and its GitHub
  Release untouched. So a triggering change without a version bump publishes nothing, and the
  Docs workflow then redeploys the site.
- To release, bump the version together in:
  - `Curated.Sdk/Curated.Sdk.csproj` (`<Version>`),
  - `engines/unity/com.curatedgames.sdk/package.json` (`version`; packing fails if it differs
    from the csproj),
  - and, when that binding changed, its own version: `engines/godot/addons/curated/plugin.cfg`
    (`version`), `engines/unreal/CuratedSDK/CuratedSDK.uplugin` (`VersionName` and the integer
    `Version`), and the `@plugindesc v...` header of `engines/rpgmaker/CuratedSDK.js`. Nothing
    checks these three against the csproj; release zips are named with the csproj version.
- Release notes: see [Changelog](README.md#changelog) in the README.
