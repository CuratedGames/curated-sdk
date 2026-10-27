extends Node
## Curated autoload (registered as "Curated" by the plugin).
##
## var result := await Curated.init_from_launcher(1002)
## if result.ok: print(result.session.username)
## else: push_warning(result.error)  # contract error code, e.g. "not_entitled"

const Contract := preload("res://addons/curated/curated_contract.gd")

signal launch_completed(result: Dictionary)

## Authenticated, entitled session after a successful init_from_launcher, else {}.
var session: Dictionary = {}

var _context: Dictionary = {}
var _transport: Callable = Callable()


## True in exports made with the "Dedicated Server" preset; Curated never initializes there.
func is_dedicated_server() -> bool:
	return OS.has_feature("dedicated_server")


## Read launch variables, verify the session, and confirm ownership of `app_id`.
## Returns { ok = true, session } or { ok = false, error }.
## `env` and `transport` are for tests; transport is Callable(method, url, token) returning
## { status, text } or { network_error = true }.
func init_from_launcher(app_id: int, env = null, transport: Callable = Callable()) -> Dictionary:
	session = {}
	_context = {}
	if env == null and is_dedicated_server():
		return _finish({"ok": false, "error": Contract.ERR_NOT_LAUNCHED})
	var parsed := Contract.parse_launch_env(Contract.environment_from_os() if env == null else env, app_id)
	if not parsed.ok:
		return _finish(parsed)
	var context: Dictionary = parsed.context
	_transport = transport

	var me := await _call(context, HTTPClient.METHOD_GET, "/auth/me")
	if not me.ok:
		return _finish(me)
	if typeof(me.body) != TYPE_DICTIONARY or typeof(me.body.get("user_id", null)) != TYPE_STRING:
		return _finish({"ok": false, "error": Contract.ERR_HUB})
	var curated_id := str(me.body.get("curated_id", "")).strip_edges()
	if curated_id.is_empty():
		return _finish({"ok": false, "error": Contract.ERR_HUB})

	var lib := await _call(context, HTTPClient.METHOD_GET, "/library")
	if not lib.ok:
		return _finish(lib)
	var entitlement := Contract.find_entitlement(lib.body, context.app_id)
	if entitlement.is_empty():
		return _finish({"ok": false, "error": Contract.ERR_HUB})
	if not entitlement.entitled:
		return _finish({"ok": false, "error": Contract.ERR_NOT_ENTITLED})

	_context = context
	session = {
		"app_id": context.app_id,
		"api_base": context.api_base,
		"user_id": me.body.user_id,
		"curated_id": curated_id,
		"username": str(me.body.get("username", "")),
		"language": context.language,
		"game_language": Contract.game_language_from_env({"CURATED_LANGUAGE": context.language}),
		"entitlement": entitlement,
	}
	return _finish({"ok": true, "session": session})


## Game language the player selected in the Curated launcher, as a lowercase Curated code
## ("en", "es", "it", "pt-br", ...). It is the launcher's client language, the same one used to
## pick language depots. Returns "en" when the game was not started by the launcher, no language
## was sent, or it is not a Curated game language. Works before init_from_launcher. `env` is for tests.
func get_current_game_language(env = null) -> String:
	return Contract.game_language_from_env(Contract.environment_from_os() if env == null else env)


## Migrating from Steam: get_current_game_language() as a Steam client language API name
## ("english", "spanish", "italian", ...).
func get_current_game_language_steam_api_name(env = null) -> String:
	return Contract.steam_api_language_name(get_current_game_language(env))


## Re-check ownership during a long session. Returns { ok, entitlement } or { ok = false, error }.
func check_entitlement() -> Dictionary:
	if _context.is_empty():
		return {"ok": false, "error": Contract.ERR_NOT_LAUNCHED}
	var lib := await _call(_context, HTTPClient.METHOD_GET, "/library")
	if not lib.ok:
		return lib
	var entitlement := Contract.find_entitlement(lib.body, _context.app_id)
	if entitlement.is_empty():
		return {"ok": false, "error": Contract.ERR_HUB}
	return {"ok": true, "entitlement": entitlement}


## Unlock an achievement by API name (idempotent). Returns { ok, newly_unlocked } or { ok = false, error }.
func unlock_achievement(api_name: String) -> Dictionary:
	if _context.is_empty():
		return {"ok": false, "error": Contract.ERR_NOT_LAUNCHED}
	var trimmed := api_name.strip_edges()
	if trimmed.is_empty():
		return {"ok": false, "error": Contract.ERR_HUB}
	var path := "/games/app/%d/achievements/%s/unlock" % [_context.app_id, trimmed.uri_encode()]
	var res := await _call(_context, HTTPClient.METHOD_POST, path)
	if not res.ok:
		return res
	var newly := false
	if typeof(res.body) == TYPE_DICTIONARY:
		newly = bool(res.body.get("newly_unlocked", false))
		if newly:
			_notify_overlay_achievement(res.body)
	return {"ok": true, "newly_unlocked": newly}


## Fire-and-forget toast notify to the Curated launcher overlay (optional launch env).
func _notify_overlay_achievement(body: Dictionary) -> void:
	var endpoint := OS.get_environment("CURATED_OVERLAY_ENDPOINT").strip_edges()
	var token := OS.get_environment("CURATED_OVERLAY_TOKEN").strip_edges()
	if endpoint.is_empty() or token.is_empty():
		return
	var colon := endpoint.rfind(":")
	if colon <= 0:
		return
	var host := endpoint.substr(0, colon).strip_edges()
	var port := int(endpoint.substr(colon + 1).strip_edges())
	if host.is_empty() or port <= 0:
		return
	var api_name := str(body.get("api_name", "")).strip_edges()
	var display_name := str(body.get("display_name", api_name))
	var description := str(body.get("description", ""))
	var icon_url := str(body.get("icon_achieved_uri", "")).strip_edges()
	var app_id := int(body.get("app_id", _context.app_id))
	var payload := {
		"v": 1,
		"token": token,
		"type": "achievement_unlocked",
		"app_id": app_id,
		"api_name": api_name,
		"display_name": display_name,
		"description": description,
	}
	if not icon_url.is_empty():
		payload["icon_url"] = icon_url
	if body.has("global_unlock_percent") and body["global_unlock_percent"] != null:
		payload["global_unlock_percent"] = body["global_unlock_percent"]
	var peer := StreamPeerTCP.new()
	if peer.connect_to_host(host, port) != OK:
		return
	var deadline := Time.get_ticks_msec() + 750
	while peer.get_status() == StreamPeerTCP.STATUS_CONNECTING and Time.get_ticks_msec() < deadline:
		peer.poll()
		OS.delay_msec(5)
	if peer.get_status() != StreamPeerTCP.STATUS_CONNECTED:
		peer.disconnect_from_host()
		return
	var json := JSON.stringify(payload) + "\n"
	peer.put_data(json.to_utf8_buffer())
	peer.disconnect_from_host()


## Load stat definitions and current player values. Returns { ok, stats } or { ok = false, error }.
func refresh_stats() -> Dictionary:
	if _context.is_empty():
		return {"ok": false, "error": Contract.ERR_NOT_LAUNCHED}
	var path := "/games/app/%d/stats/progress" % _context.app_id
	var res := await _call(_context, HTTPClient.METHOD_GET, path)
	if not res.ok:
		return res
	var stats: Array = []
	if typeof(res.body) == TYPE_DICTIONARY and typeof(res.body.get("stats", null)) == TYPE_ARRAY:
		stats = res.body.stats
	return {"ok": true, "stats": stats}


## SetStat absolute value. Returns { ok, stat, previous_value } or { ok = false, error }.
func set_stat(api_name: String, value: float) -> Dictionary:
	if _context.is_empty():
		return {"ok": false, "error": Contract.ERR_NOT_LAUNCHED}
	var trimmed := api_name.strip_edges()
	if trimmed.is_empty():
		return {"ok": false, "error": Contract.ERR_HUB}
	var path := "/games/app/%d/stats/%s" % [_context.app_id, trimmed.uri_encode()]
	var res := await _call(_context, HTTPClient.METHOD_PUT, path, {"value": value})
	if not res.ok:
		return res
	return {
		"ok": true,
		"stat": res.body.get("stat", {}) if typeof(res.body) == TYPE_DICTIONARY else {},
		"previous_value": res.body.get("previous_value", null) if typeof(res.body) == TYPE_DICTIONARY else null,
	}


## Increment a player stat. Returns { ok, stat, previous_value } or { ok = false, error }.
func increment_stat(api_name: String, delta: float = 1.0) -> Dictionary:
	if _context.is_empty():
		return {"ok": false, "error": Contract.ERR_NOT_LAUNCHED}
	var trimmed := api_name.strip_edges()
	if trimmed.is_empty():
		return {"ok": false, "error": Contract.ERR_HUB}
	var path := "/games/app/%d/stats/%s/increment" % [_context.app_id, trimmed.uri_encode()]
	var res := await _call(_context, HTTPClient.METHOD_POST, path, {"delta": delta})
	if not res.ok:
		return res
	return {
		"ok": true,
		"stat": res.body.get("stat", {}) if typeof(res.body) == TYPE_DICTIONARY else {},
		"previous_value": res.body.get("previous_value", null) if typeof(res.body) == TYPE_DICTIONARY else null,
	}


## Batch StoreStats. `updates` is an array of { api_name, value } and/or { api_name, delta }.
func store_stats(updates: Array) -> Dictionary:
	if _context.is_empty():
		return {"ok": false, "error": Contract.ERR_NOT_LAUNCHED}
	if updates.is_empty():
		return {"ok": false, "error": Contract.ERR_HUB}
	var path := "/games/app/%d/stats/store" % _context.app_id
	var res := await _call(_context, HTTPClient.METHOD_POST, path, {"stats": updates})
	if not res.ok:
		return res
	return {"ok": true, "body": res.body}


## Mint a short-lived OpenID Connect ID token. Never log it. Returns { ok, id_token } or { ok = false, error }.
func get_openid_id_token() -> Dictionary:
	if _context.is_empty():
		return {"ok": false, "error": Contract.ERR_NOT_LAUNCHED}
	var res := await _call(_context, HTTPClient.METHOD_POST, "/auth/openid/id-token", {"app_id": _context.app_id})
	if not res.ok:
		return res
	var token := ""
	if typeof(res.body) == TYPE_DICTIONARY:
		token = str(res.body.get("id_token", "")).strip_edges()
	if token.is_empty():
		return {"ok": false, "error": Contract.ERR_HUB}
	return {"ok": true, "id_token": token}


func _finish(result: Dictionary) -> Dictionary:
	launch_completed.emit(result)
	return result


func _verb(method: int) -> String:
	match method:
		HTTPClient.METHOD_GET:
			return "GET"
		HTTPClient.METHOD_PUT:
			return "PUT"
		_:
			return "POST"


func _call(context: Dictionary, method: int, path: String, json_body = null) -> Dictionary:
	var url: String = context.api_base + path
	var verb := _verb(method)
	var body_text := ""
	if method != HTTPClient.METHOD_GET:
		body_text = "{}" if json_body == null else JSON.stringify(json_body)
	var response: Dictionary
	if _transport.is_valid():
		response = await _transport.call(verb, url, context.token, body_text)
	else:
		response = await _http(method, url, context.token, body_text)
	if response.get("network_error", false):
		return {"ok": false, "error": Contract.ERR_NETWORK}
	var error := Contract.classify_status(int(response.get("status", 0)))
	if not error.is_empty():
		return {"ok": false, "error": error}
	var text: String = response.get("text", "")
	if text.is_empty():
		return {"ok": true, "body": null}
	var json := JSON.new()
	if json.parse(text) != OK:
		return {"ok": false, "error": Contract.ERR_HUB}
	return {"ok": true, "body": json.data}


func _http(method: int, url: String, token: String, body_text: String = "") -> Dictionary:
	var http := HTTPRequest.new()
	http.timeout = 15.0
	add_child(http)
	var headers := PackedStringArray(["Authorization: Bearer %s" % token, "Accept: application/json"])
	if method != HTTPClient.METHOD_GET:
		headers.append("Content-Type: application/json")
		if body_text.is_empty():
			body_text = "{}"
	if http.request(url, headers, method, body_text) != OK:
		http.queue_free()
		return {"network_error": true}
	var completed: Array = await http.request_completed
	http.queue_free()
	if completed[0] != HTTPRequest.RESULT_SUCCESS:
		return {"network_error": true}
	return {"status": completed[1], "text": (completed[3] as PackedByteArray).get_string_from_utf8()}
