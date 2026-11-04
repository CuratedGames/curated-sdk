# Launch contract

The launch contract is how the Curated launcher starts a game and how the game checks the
player. Official bindings implement it for you. Read this page if you integrate an engine
without a binding, or want to know what the SDK does.

The full specification lives in the repository:
[`contracts/launch-contract.md`](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/launch-contract.md),
with an [OpenAPI subset](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/openapi/game-sdk.v1.yaml)
and the fixtures
[conformance.v1.json](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/fixtures/conformance.v1.json)
and [game-language.v1.json](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/fixtures/game-language.v1.json).

## Environment variables

| Variable | Required | Example | Meaning |
| --- | --- | --- | --- |
| `CURATED_LAUNCHER` | yes | `1` | The game was started by the Curated launcher |
| `CURATED_CONTRACT_VERSION` | yes | `1` | Contract version the launcher speaks |
| `CURATED_APP_ID` | yes | `1002` | App id the player launched. Must equal your own |
| `CURATED_SESSION_TOKEN` | yes | (secret) | Bearer token for this player and this game, 12 hours max |
| `CURATED_API_BASE` | no | `https://api.curated.games` | API origin. The only place the token may go |
| `CURATED_USER_ID`, `CURATED_USERNAME` | no | | Display hints. Trust `/auth/me` instead |
| `CURATED_LANGUAGE` | no | `it` | Launcher client language. See [Game language](./game-language) |
| `CURATED_OVERLAY_ENDPOINT`, `CURATED_OVERLAY_TOKEN` | no | `127.0.0.1:4050` | Launcher overlay. Achievement toasts, the in-game purchase handoff, and opening a store page |

## Startup sequence

```
read env  ->  validate version + app id  ->  GET /auth/me  ->  GET /library  ->  ready
```

1. If `CURATED_LAUNCHER` is not `1` or the token is missing: `not_launched_by_curated`.
2. If the contract version is below your minimum (currently `1`) or not a number:
   `unsupported_contract_version`. Newer launcher versions are accepted.
3. If `CURATED_APP_ID` is missing or differs from your app id: `app_mismatch`.
4. `GET /auth/me` with `Authorization: Bearer <token>`. 401 is `session_invalid`.
5. `GET /library`. No active item for your app is `not_entitled`.

No response at all is `network_error`. Any other non-2xx response is `hub_error`.

## Token scope

The token works only for the game-facing operations in the OpenAPI file, and only for your app.
It expires after at most 12 hours, and right away when the player signs out of the launcher.
Treat a later 401 as `session_invalid` and ask the player to relaunch.

## Store overlay

`Friends.OpenStoreOverlay` asks the launcher overlay to open a published store page. The game
sends one `open_store_overlay` line (`app_id`, `flag` 0, 1, or 2) and reads one
`open_store_overlay_result` line. `{"v":1,"type":"open_store_overlay_result","ok":true}` means
the overlay opened it. `{"v":1,"type":"open_store_overlay_result","ok":false,"error":"<English message>"}`
is the failure, and `error` is the text the caller sees. Flag 0 opens the page. Flags 1 and 2
add the app to the cart and are only for a released or preorderable app. The
[launch contract](https://github.com/CuratedGames/curated-sdk/blob/main/contracts/launch-contract.md#overlay-store-page)
has the full line format. C# games call [Store overlay](./dotnet#store-overlay) and do not open
this socket themselves.

## Command-line arguments

Players can add launch options per game in the launcher (**Library > game > Properties >
General > Launch options**). They arrive as ordinary arguments. Credentials are never on the
command line, so you can log arguments safely.

## Versioning

Breaking changes create contract v2 with its own spec. Bindings check a minimum version, not an
exact match, so shipped games keep launching when the launcher updates.
