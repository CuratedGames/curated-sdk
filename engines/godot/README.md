# Curated SDK for Godot 4

GDScript addon implementing launch contract v1 (`contracts/launch-contract.md`). Works in
GDScript and C# projects; C# projects can also reference `Curated.Sdk.dll` (netstandard2.1)
for Cloud Save, friends, and presence.

## Install

1. Download `curated-godot-<version>.zip` from the curated-sdk Releases.
2. Unzip it into the project root so the files land in `res://addons/curated/`.
3. **Project > Project Settings > Plugins**: enable **Curated SDK**. This registers the
   `Curated` autoload.

## Use

```gdscript
func _ready() -> void:
	var result: Dictionary = await Curated.init_from_launcher(1002)
	if not result.ok:
		# result.error: not_launched_by_curated, not_entitled, session_invalid, ...
		get_tree().quit()
		return
	print("Signed in as %s" % result.session.username)

func _on_boss_defeated() -> void:
	await Curated.unlock_achievement("BOSS_1")
```

`Curated.check_entitlement()` re-checks ownership during long sessions. `Curated.session`
holds `user_id`, `username`, `language` (raw launcher value), `game_language`, and `entitlement`
after a successful init.

## Game language

```gdscript
var language := Curated.get_current_game_language()  # "en", "es", "it", "pt-br", ...
TranslationServer.set_locale(language)
var steam_name := Curated.get_current_game_language_steam_api_name()  # "english", "italian", ...
```

It returns the language the player selected in the Curated launcher (the language the launcher
downloads depots for), or `"en"` when the game was not started by the launcher. It works before
`init_from_launcher`.

## Dedicated servers

`init_from_launcher` returns `not_launched_by_curated` without touching the network when the
build has the `dedicated_server` feature (Godot's **Dedicated Server** export preset adds it),
so server exports can keep the addon: it stays inert and never reads launch variables. Skip
your own Curated calls there with `if not OS.has_feature("dedicated_server")`.

## Tests

From a Godot 4 project containing the addon:

```bash
godot --headless --script res://addons/curated/tests/run_conformance.gd -- /path/to/contracts/fixtures/conformance.v1.json
godot --headless --script res://addons/curated/tests/run_game_language.gd -- /path/to/contracts/fixtures/game-language.v1.json
```
