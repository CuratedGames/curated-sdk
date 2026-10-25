# Curated launch contract (v1)

Every Curated engine binding (C# / Unity, Unreal, Godot, RPG Maker, or your own) implements this
contract. It describes how the Curated launcher starts a game, how the game authenticates the
player, and how the game checks that the player owns it. Nothing here depends on a specific
engine or language: if your runtime can read environment variables and send HTTPS requests, it
can integrate with Curated.

| Item | Value |
|------|-------|
| Contract version | `1` |
| Transport | HTTPS, JSON bodies, `Authorization: Bearer <token>` |
| Default API base | `https://api.curated.games` |
| Machine-readable API | [`openapi/game-sdk.v1.yaml`](openapi/game-sdk.v1.yaml) |
| Conformance cases | [`fixtures/conformance.v1.json`](fixtures/conformance.v1.json) |
| Game language cases | [`fixtures/game-language.v1.json`](fixtures/game-language.v1.json) |

## 1. Launcher to game: environment variables

When a player presses **Play** on a Curated title, the launcher starts the game executable with
these environment variables set on the child process. They are never written to disk by the
launcher.

| Variable | Required | Example | Meaning |
|----------|----------|---------|---------|
| `CURATED_LAUNCHER` | yes | `1` | Marker: the game was started by the Curated launcher. |
| `CURATED_CONTRACT_VERSION` | yes | `1` | Contract version the launcher speaks. |
| `CURATED_APP_ID` | yes | `1002` | Curated app id the player launched. |
| `CURATED_SESSION_TOKEN` | yes | `9f2c...` | Opaque bearer token for the signed-in player, scoped to this game (see [Token scope](#token-scope)). Secret. |
| `CURATED_API_BASE` | no | `https://api.curated.games` | API origin. Use the default when unset or blank. |
| `CURATED_USER_ID` | no | `0b4c...-uuid` | Player id hint. Authoritative value comes from the session call. |
| `CURATED_USERNAME` | no | `paolo` | Player display name hint. |
| `CURATED_LANGUAGE` | no | `it` | Game language the player selected in the launcher: the launcher's client language (`en`, `es`, `it` today). The launcher downloads language depots for this same language. See [Game language](#game-language). |

Rules for bindings:

1. Read the variables once at startup. Trim whitespace. Treat blank values as missing.
2. If `CURATED_LAUNCHER` is not `1` or `CURATED_SESSION_TOKEN` is missing, report
   `not_launched_by_curated`. Do not prompt for a password inside the game.
3. Parse `CURATED_CONTRACT_VERSION` as an integer. Report `unsupported_contract_version` when it
   is higher than the newest version the binding supports, or not a positive integer.
   Additive changes (new optional variables, new response fields) never bump the version.
4. Every game ships with its own Curated app id compiled in. If `CURATED_APP_ID` is missing, is
   not a positive integer, or does not match it, report `app_mismatch`. This stops a launch meant
   for one title from being picked up by another.
5. `CURATED_API_BASE` must be an absolute `https` (or `http` for local development) URL. Strip a
   trailing slash. Any other value is `not_launched_by_curated`.
6. Only send the token to `CURATED_API_BASE` (or the default). Never log it, never persist it,
   never pass it to third-party services or child processes.

### Game language

Bindings expose the current game language like Steamworks `ISteamApps::GetCurrentGameLanguage`
(for example `CuratedClient.GetCurrentGameLanguage()` in C#, `Curated.get_current_game_language()`
in Godot). It is read from `CURATED_LANGUAGE`, needs no network call, and works before the
startup sequence below.

- Value: a lowercase Curated language code, the same codes depots and the Curated language catalog
  use: `en`, `es`, `it`, and any other code in
  [`fixtures/game-language.v1.json`](fixtures/game-language.v1.json) (`pt-br`, `zh-cn`, `es-419`, ...).
- Source: the launcher's client language, which follows the player's account main language. It is
  the language the launcher uses to pick which language depots to download, so the game always
  asks for content that is installed. The launcher sends `en`, `es` or `it` today; bindings accept
  every catalog language so games keep working when the launcher adds more.
- Normalization: trim, lowercase, `_` becomes `-`; match a code, then an alias (`zh` is `zh-cn`,
  `nb` is `no`), then a Steam API name (`italian`), then the primary subtag (`it-IT` is `it`).
- Fallback: `en` when the variable is missing or blank (game started outside the launcher, older
  launcher) or the value is not a Curated game language.
- Steam API names: bindings also return the Steam API language name (`english`, `spanish`,
  `italian`, `brazilian`, `koreana`, ...) for code ported from Steamworks, with `english` as the
  fallback.

Bindings check themselves against every case in `fixtures/game-language.v1.json`.

### Token scope

The launcher never hands a game the player's account session. On each Play it requests a token
scoped to the launched app. That token:

- Works only on the game-facing operations in the OpenAPI file, and only for this app. Calls for
  another app return `403`; every other Curated endpoint (store, cart, wallet, account settings)
  returns `401`.
- Expires after at most 12 hours, and immediately when the player signs out of the launcher.
  Long sessions should treat a `401` as `session_invalid` and ask the player to relaunch.

Bindings need no special handling: the same variable, endpoints, and error codes apply.

### Command-line arguments

Players can add their own launch options per game in the launcher (Library, game, Properties,
General, Launch options). The launcher appends them to the game's argument list exactly as typed,
split on whitespace with double quotes grouping. The launcher never puts Curated credentials on
the command line, so games can log their arguments safely.

### Development without the launcher

Set the same variables in your IDE or shell to run a development build against Curated. Use a
session token from your own signed-in account (for example the `token` that
`curated-content-builder login` caches in `%USERPROFILE%\.curated\credentials.json`). Game-facing
operations accept it the same way as a launcher-issued game token, but it is not limited to one
game, so keep it out of builds you share. Your account needs an entitlement for the app.

## 2. Game to Curated: authentication

```
GET {api_base}/auth/me
Authorization: Bearer {CURATED_SESSION_TOKEN}
```

| Status | Binding result |
|--------|----------------|
| `200` | Authenticated. Read `user_id` (uuid) and `username` from the body. |
| `401` | `session_invalid`. The session expired or the player signed out. Ask the player to relaunch from Curated. |
| other | `hub_error` (5xx) or `network_error` (no response). Retry with backoff. |

The response also contains account fields (for example `email`, which is empty for game-scoped
tokens) that games must ignore. Bindings expose only `user_id` and `username`.

## 3. Game to Curated: entitlement

```
GET {api_base}/library
Authorization: Bearer {CURATED_SESSION_TOKEN}
```

The body is an array of the player's **active** entitlements. With a game-scoped token it holds at
most the entry for this app; with a development account token it holds every owned game. Either
way, the player owns the game when an item's `app_id` equals the game's app id. Bindings report:

| Field | Source |
|-------|--------|
| `entitled` | `true` when a matching item exists |
| `source` | Matching item's `source`: `primary`, `resale`, `grant`, or `bonus` |
| `acquired_at` | Matching item's `acquired_at` (RFC 3339) |

When no item matches, report `not_entitled`. What the game does next is a product decision; the
recommended default is to show a short message and quit. Refunds, resales, and staff revocations
deactivate entitlements, so re-check at least once per launch, not only at install time.

## 4. Startup sequence

```
read env  ->  validate version + app id  ->  GET /auth/me  ->  GET /library  ->  ready
```

Bindings expose this as a single call (for example `InitFromLauncherAsync` in C#,
`init_from_launcher()` in Godot) that resolves to either a ready session or one error code.

## 5. Error codes

All bindings use these exact strings so support logs read the same across engines.

| Code | When |
|------|------|
| `not_launched_by_curated` | Launch variables missing. |
| `unsupported_contract_version` | Launcher speaks a newer, incompatible contract. |
| `app_mismatch` | `CURATED_APP_ID` is not this game's app id. |
| `session_invalid` | `/auth/me` returned 401. |
| `not_entitled` | `/library` has no active item for this app. |
| `network_error` | No HTTP response (offline, DNS, TLS). |
| `hub_error` | Any other non-2xx response. |

## 6. Other features

Achievements, Cloud Save, friends, and presence use the same bearer token and are listed in the
OpenAPI file. They all require an active entitlement for the app on the server side, so a binding
that skips the entitlement check still cannot unlock achievements for players who do not own the
game. A game-scoped token gets `403` on another app's routes.

## 7. Dedicated servers

Server builds must not include or initialize Curated bindings. Each engine package documents how
it excludes itself (Unity define constraints, Unreal target deny list, and so on).

## 8. Versioning

- Breaking changes create contract `v2` with its own spec file. The launcher keeps setting the
  v1 variables until v1 is formally retired, so shipped games keep working.
- The conformance file is append-only for a given version.
