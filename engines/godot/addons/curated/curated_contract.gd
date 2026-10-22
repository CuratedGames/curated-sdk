extends RefCounted
## Curated launch contract v1: pure parsing and classification helpers (no networking).

const CONTRACT_VERSION := 1
const DEFAULT_API_BASE := "https://api.curated.games"

const ERR_NOT_LAUNCHED := "not_launched_by_curated"
const ERR_UNSUPPORTED_VERSION := "unsupported_contract_version"
const ERR_APP_MISMATCH := "app_mismatch"
const ERR_SESSION_INVALID := "session_invalid"
const ERR_NOT_ENTITLED := "not_entitled"
const ERR_NETWORK := "network_error"
const ERR_HUB := "hub_error"

const VARIABLES := [
	"CURATED_LAUNCHER",
	"CURATED_CONTRACT_VERSION",
	"CURATED_APP_ID",
	"CURATED_SESSION_TOKEN",
	"CURATED_API_BASE",
	"CURATED_USER_ID",
	"CURATED_USERNAME",
	"CURATED_LANGUAGE",
]


## Launch variables of the current process.
static func environment_from_os() -> Dictionary:
	var env := {}
	for name in VARIABLES:
		if OS.has_environment(name):
			env[name] = OS.get_environment(name)
	return env


## Trimmed value, or "" when missing or blank.
static func read_var(env: Dictionary, name: String) -> String:
	var raw = env.get(name, null)
	if typeof(raw) != TYPE_STRING:
		return ""
	return String(raw).strip_edges()


static func _is_positive_integer(text: String) -> bool:
	if text.is_empty():
		return false
	for c in text:
		if c < "0" or c > "9":
			return false
	return int(text) > 0


## Returns { ok = true, context = {...} } or { ok = false, error = <code> }.
static func parse_launch_env(env: Dictionary, expected_app_id: int) -> Dictionary:
	var token := read_var(env, "CURATED_SESSION_TOKEN")
	if read_var(env, "CURATED_LAUNCHER") != "1" or token.is_empty():
		return {"ok": false, "error": ERR_NOT_LAUNCHED}
	var version := read_var(env, "CURATED_CONTRACT_VERSION")
	if not _is_positive_integer(version) or int(version) > CONTRACT_VERSION:
		return {"ok": false, "error": ERR_UNSUPPORTED_VERSION}
	var app_id := read_var(env, "CURATED_APP_ID")
	if not _is_positive_integer(app_id) or int(app_id) != expected_app_id:
		return {"ok": false, "error": ERR_APP_MISMATCH}
	var api_base := DEFAULT_API_BASE
	var api_raw := read_var(env, "CURATED_API_BASE")
	if not api_raw.is_empty():
		while api_raw.ends_with("/"):
			api_raw = api_raw.substr(0, api_raw.length() - 1)
		var lower := api_raw.to_lower()
		var has_scheme := lower.begins_with("https://") or lower.begins_with("http://")
		var host := api_raw.substr(api_raw.find("://") + 3) if has_scheme else ""
		if not has_scheme or host.is_empty() or host.contains(" "):
			return {"ok": false, "error": ERR_NOT_LAUNCHED}
		api_base = api_raw
	var language = read_var(env, "CURATED_LANGUAGE")
	return {
		"ok": true,
		"context": {
			"contract_version": int(version),
			"app_id": int(app_id),
			"token": token,
			"api_base": api_base,
			"user_id_hint": read_var(env, "CURATED_USER_ID"),
			"username_hint": read_var(env, "CURATED_USERNAME"),
			"language": null if language.is_empty() else language,
		},
	}


## Maps an HTTP status to "" (success) or a contract error code.
static func classify_status(status: int) -> String:
	if status == 401:
		return ERR_SESSION_INVALID
	if status < 200 or status >= 300:
		return ERR_HUB
	return ""


## Returns { entitled, source, acquired_at } or {} when the body is not a list.
static func find_entitlement(items, app_id: int) -> Dictionary:
	if typeof(items) != TYPE_ARRAY:
		return {}
	for item in items:
		if typeof(item) == TYPE_DICTIONARY and int(item.get("app_id", 0)) == app_id:
			return {
				"entitled": true,
				"source": item.get("source", null),
				"acquired_at": item.get("acquired_at", null),
			}
	return {"entitled": false, "source": null, "acquired_at": null}
