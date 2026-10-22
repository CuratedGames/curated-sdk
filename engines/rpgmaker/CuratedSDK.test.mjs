// Runs contracts/fixtures/conformance.v1.json against CuratedSDK.js (node --test).
import { test } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const Curated = require("./CuratedSDK.js");
const fixture = JSON.parse(
  readFileSync(join(here, "..", "..", "contracts", "fixtures", "conformance.v1.json"), "utf8"),
);

function fixtureTransport(responses, log) {
  return async (method, url, token) => {
    log.push({ line: `${method} ${url}`, authorization: `Bearer ${token}` });
    const path = new URL(url).pathname;
    const response = responses[path];
    if (!response) throw new Error(`unexpected request ${url}`);
    if (response.network_error) throw new Error("simulated network failure");
    return { status: response.status, text: JSON.stringify(response.body) };
  };
}

test("fixture targets this plugin's contract version", () => {
  assert.equal(fixture.contract_version, Curated.CONTRACT_VERSION);
  assert.equal(fixture.default_api_base, Curated.DEFAULT_API_BASE);
});

for (const c of fixture.cases) {
  test(`conformance: ${c.name}`, async () => {
    const log = [];
    const result = await Curated.initFromLauncher(fixture.game_app_id, {
      env: c.env,
      transport: fixtureTransport(c.responses, log),
    });
    assert.deepEqual(
      log.map((r) => r.line),
      c.expect.requests,
    );
    if (c.expect.ok) {
      assert.equal(result.ok, true, `expected success, got ${result.error}`);
      for (const r of log) assert.equal(r.authorization, c.expect.authorization);
      const s = result.session;
      assert.equal(s.appId, c.expect.app_id);
      assert.equal(s.apiBase, c.expect.api_base);
      assert.equal(s.language, c.expect.language);
      assert.equal(s.userId, c.expect.user_id);
      assert.equal(s.username, c.expect.username);
      assert.equal(s.entitlement.entitled, c.expect.entitled);
      assert.equal(s.entitlement.source, c.expect.source);
      assert.equal(s.entitlement.acquiredAt, c.expect.acquired_at);
      assert.equal(Curated.session, s);
    } else {
      assert.equal(result.ok, false);
      assert.equal(result.error, c.expect.error);
      assert.equal(Curated.session, null);
    }
  });
}

test("unlockAchievement posts to the app achievement and reports newly unlocked", async () => {
  const log = [];
  const transport = async (method, url) => {
    log.push(`${method} ${url}`);
    const path = new URL(url).pathname;
    if (path === "/auth/me") return { status: 200, text: '{"user_id":"u","username":"p"}' };
    if (path === "/library") return { status: 200, text: '[{"app_id":1002,"source":"grant"}]' };
    return { status: 200, text: '{"newly_unlocked":true}' };
  };
  const env = {
    CURATED_LAUNCHER: "1",
    CURATED_CONTRACT_VERSION: "1",
    CURATED_APP_ID: "1002",
    CURATED_SESSION_TOKEN: "tok",
  };
  assert.equal((await Curated.initFromLauncher(1002, { env, transport })).ok, true);
  const unlocked = await Curated.unlockAchievement("win level/1");
  assert.deepEqual(unlocked, { ok: true, newlyUnlocked: true });
  assert.equal(log.at(-1), "POST https://api.curated.games/games/app/1002/achievements/win%20level%2F1/unlock");
});

test("checkEntitlement and unlock fail before a successful launch", async () => {
  await Curated.initFromLauncher(1002, { env: {}, transport: async () => ({ status: 500, text: "" }) });
  assert.deepEqual(await Curated.checkEntitlement(), { ok: false, error: "not_launched_by_curated" });
  assert.deepEqual(await Curated.unlockAchievement("x"), { ok: false, error: "not_launched_by_curated" });
});
