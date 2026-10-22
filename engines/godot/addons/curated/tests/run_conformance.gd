extends SceneTree
## Runs contracts/fixtures/conformance.v1.json against the Godot binding.
##
## From a Godot 4 project that contains addons/curated:
##   godot --headless --script res://addons/curated/tests/run_conformance.gd -- <path to conformance.v1.json>
## Exits with code 0 when every case passes.

const CuratedNode := preload("res://addons/curated/curated.gd")
const Contract := preload("res://addons/curated/curated_contract.gd")

var _requests: Array = []
var _authorizations: Array = []
var _responses: Dictionary = {}


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		printerr("Pass the path to conformance.v1.json after --")
		quit(2)
		return
	var fixture = JSON.parse_string(FileAccess.get_file_as_string(args[0]))
	if typeof(fixture) != TYPE_DICTIONARY:
		printerr("Could not read fixture")
		quit(2)
		return
	var failures := 0
	if int(fixture.contract_version) != Contract.CONTRACT_VERSION or fixture.default_api_base != Contract.DEFAULT_API_BASE:
		printerr("FAIL fixture header")
		failures += 1

	var curated: Node = CuratedNode.new()
	root.add_child(curated)
	for c in fixture.cases:
		var problem := await _run_case(curated, int(fixture.game_app_id), c)
		if problem.is_empty():
			print("ok   %s" % c.name)
		else:
			printerr("FAIL %s: %s" % [c.name, problem])
			failures += 1
	print("%d case(s) failed" % failures)
	quit(1 if failures > 0 else 0)


func _fake_transport(method: String, url: String, token: String) -> Dictionary:
	_requests.append("%s %s" % [method, url])
	_authorizations.append("Bearer %s" % token)
	var path := url.substr(url.find("/", url.find("://") + 3))
	var response: Dictionary = _responses.get(path, {})
	if response.is_empty() or response.get("network_error", false):
		return {"network_error": true}
	return {"status": int(response.status), "text": JSON.stringify(response.body)}


func _run_case(curated: Node, app_id: int, c: Dictionary) -> String:
	_requests = []
	_authorizations = []
	_responses = c.responses
	var result: Dictionary = await curated.init_from_launcher(app_id, c.env, _fake_transport)
	var expect: Dictionary = c.expect
	if _requests != expect.requests:
		return "requests %s != %s" % [_requests, expect.requests]
	if not expect.ok:
		if result.ok or result.error != expect.error:
			return "expected error %s, got %s" % [expect.error, result]
		if not curated.session.is_empty():
			return "session should be empty after failure"
		return ""
	if not result.ok:
		return "expected success, got %s" % result.error
	for auth in _authorizations:
		if auth != expect.authorization:
			return "authorization %s" % auth
	var s: Dictionary = result.session
	var checks := {
		"app_id": [s.app_id, int(expect.app_id)],
		"api_base": [s.api_base, expect.api_base],
		"language": [s.language, expect.language],
		"user_id": [s.user_id, expect.user_id],
		"username": [s.username, expect.username],
		"entitled": [s.entitlement.entitled, expect.entitled],
		"source": [s.entitlement.source, expect.source],
		"acquired_at": [s.entitlement.acquired_at, expect.acquired_at],
	}
	for key in checks:
		if checks[key][0] != checks[key][1]:
			return "%s %s != %s" % [key, checks[key][0], checks[key][1]]
	return ""
