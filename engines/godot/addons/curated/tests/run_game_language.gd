extends SceneTree
## Runs contracts/fixtures/game-language.v1.json against the Godot binding.
##
## From a Godot 4 project that contains addons/curated:
##   godot --headless --script res://addons/curated/tests/run_game_language.gd -- <path to game-language.v1.json>
## Exits with code 0 when every case passes.

const CuratedNode := preload("res://addons/curated/curated.gd")
const Contract := preload("res://addons/curated/curated_contract.gd")


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		printerr("Pass the path to game-language.v1.json after --")
		quit(2)
		return
	var fixture = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	if typeof(fixture) != TYPE_DICTIONARY:
		printerr("Could not read fixture")
		quit(2)
		return
	var failures := 0
	if fixture.default_language != Contract.DEFAULT_GAME_LANGUAGE:
		printerr("FAIL default language")
		failures += 1
	var languages: Array = fixture.languages
	if languages.size() != Contract.GAME_LANGUAGES.size():
		printerr("FAIL table size %d != %d" % [Contract.GAME_LANGUAGES.size(), languages.size()])
		failures += 1
	else:
		for i in languages.size():
			var e: Dictionary = languages[i]
			var a: Array = Contract.GAME_LANGUAGES[i]
			if a[0] != e.code or a[1] != e.name or a[2] != e.steam or a[3] != e.aliases:
				printerr("FAIL table row %s" % e.code)
				failures += 1

	var curated: Node = CuratedNode.new()
	root.add_child(curated)
	for c in fixture.cases:
		var language: String = curated.get_current_game_language(c.env)
		var steam: String = curated.get_current_game_language_steam_api_name(c.env)
		if language != c.expect.language or steam != c.expect.steam:
			printerr("FAIL %s: %s / %s" % [c.name, language, steam])
			failures += 1
		else:
			print("ok   %s" % c.name)
	print("%d case(s) failed" % failures)
	quit(1 if failures > 0 else 0)
