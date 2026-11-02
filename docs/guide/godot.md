# Godot 4

GDScript addon. It works in GDScript and C# projects. C# projects can also reference
`Curated.Sdk.dll` for Cloud Save, friends, and presence.

## Install

1. Download `curated-godot-%SDK_VERSION%.zip` from
   [Releases](https://github.com/CuratedGames/curated-sdk/releases/tag/%SDK_TAG%).
2. Unzip it into the project root so the files land in `res://addons/curated/`.
3. **Project > Project Settings > Plugins**: enable **Curated SDK**. This registers the
   `Curated` autoload.

## Sign in and check ownership

```gdscript
func _ready() -> void:
	var result: Dictionary = await Curated.init_from_launcher(1002)
	if not result.ok:
		# result.error: not_launched_by_curated, not_entitled, session_invalid, ...
		get_tree().quit()
		return
	print("Signed in as %s" % result.session.username)
```

::: info Not compiled in CI
Godot is not installed in CI, so GDScript snippets are checked by review. CI does check that the
addon's language table matches the fixture (`scripts/check-game-languages.py`), and that every
public function is listed in the [API reference](/reference/godot).
:::

`Curated.session` holds `user_id`, `curated_id`, `username`, `language` (raw launcher value),
`game_language`, and `entitlement` after a successful init. The `launch_completed` signal fires
with the same result.

## Features

```gdscript
func _on_boss_defeated() -> void:
	await Curated.unlock_achievement("BOSS_1")
	await Curated.set_stat("BossesDefeated", 1)
```

- `check_entitlement()` re-checks ownership during long sessions.
- `refresh_stats()`, `set_stat()`, `increment_stat()`, `store_stats()` for player stats.
- `authorize_txn(order_id, trans_id, authorized)` for [in-game purchases](./purchases).
- `get_openid_id_token()` for [OpenID Connect](./openid).
- `get_current_game_language()` for the [game language](./game-language).

Cloud Save, friends, and presence are C# only (`Curated.Sdk.dll`).

## Dedicated servers

`init_from_launcher` returns `not_launched_by_curated` without touching the network when the
export has the `dedicated_server` feature. Skip your own calls there with
`if not OS.has_feature("dedicated_server")`.

## Tests

From a Godot 4 project containing the addon:

```bash
godot --headless --script res://addons/curated/tests/run_conformance.gd -- /path/to/contracts/fixtures/conformance.v1.json
godot --headless --script res://addons/curated/tests/run_game_language.gd -- /path/to/contracts/fixtures/game-language.v1.json
```

## Reference

[Godot API reference](/reference/godot).
