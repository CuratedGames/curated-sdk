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
    if (path === "/auth/me") return { status: 200, text: '{"user_id":"u","username":"p","curated_id":"10000000000000001"}' };
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
  assert.deepEqual(await Curated.refreshStats(), { ok: false, error: "not_launched_by_curated" });
  assert.deepEqual(await Curated.setStat("x", 1), { ok: false, error: "not_launched_by_curated" });
  assert.deepEqual(await Curated.getOpenIdIdToken(), { ok: false, error: "not_launched_by_curated" });
});

test("setStat and incrementStat hit the player stats routes", async () => {
  const log = [];
  const transport = async (method, url, _token, body) => {
    log.push({ line: `${method} ${url}`, body });
    const path = new URL(url).pathname;
    if (path === "/auth/me") return { status: 200, text: '{"user_id":"u","username":"p","curated_id":"10000000000000001"}' };
    if (path === "/library") return { status: 200, text: '[{"app_id":1002,"source":"grant"}]' };
    if (path.endsWith("/increment")) {
      return { status: 200, text: '{"stat":{"api_name":"CardsCreated","value":4},"previous_value":3}' };
    }
    if (path.includes("/stats/")) {
      return { status: 200, text: '{"stat":{"api_name":"CardsCreated","value":10},"previous_value":0}' };
    }
    return { status: 500, text: "" };
  };
  const env = {
    CURATED_LAUNCHER: "1",
    CURATED_CONTRACT_VERSION: "1",
    CURATED_APP_ID: "1002",
    CURATED_SESSION_TOKEN: "tok",
  };
  assert.equal((await Curated.initFromLauncher(1002, { env, transport })).ok, true);
  const set = await Curated.setStat("CardsCreated", 10);
  assert.equal(set.ok, true);
  assert.equal(set.stat.api_name, "CardsCreated");
  assert.equal(set.previousValue, 0);
  assert.equal(log.at(-1).line, "PUT https://api.curated.games/games/app/1002/stats/CardsCreated");
  assert.deepEqual(log.at(-1).body, { value: 10 });

  const inc = await Curated.incrementStat("CardsCreated", 1);
  assert.equal(inc.ok, true);
  assert.equal(inc.previousValue, 3);
  assert.equal(log.at(-1).line, "POST https://api.curated.games/games/app/1002/stats/CardsCreated/increment");
  assert.deepEqual(log.at(-1).body, { delta: 1 });
});

test("getOpenIdIdToken posts app_id and returns id_token", async () => {
  const log = [];
  const transport = async (method, url, _token, body) => {
    log.push({ line: `${method} ${url}`, body });
    const path = new URL(url).pathname;
    if (path === "/auth/me") return { status: 200, text: '{"user_id":"u","username":"p","curated_id":"10000000000000001"}' };
    if (path === "/library") return { status: 200, text: '[{"app_id":1002,"source":"grant"}]' };
    if (path === "/auth/openid/id-token") return { status: 200, text: '{"id_token":"jwt.example"}' };
    return { status: 500, text: "" };
  };
  const env = {
    CURATED_LAUNCHER: "1",
    CURATED_CONTRACT_VERSION: "1",
    CURATED_APP_ID: "1002",
    CURATED_SESSION_TOKEN: "tok",
  };
  assert.equal((await Curated.initFromLauncher(1002, { env, transport })).ok, true);
  const token = await Curated.getOpenIdIdToken();
  assert.deepEqual(token, { ok: true, idToken: "jwt.example" });
  assert.equal(log.at(-1).line, "POST https://api.curated.games/auth/openid/id-token");
  assert.deepEqual(log.at(-1).body, { app_id: 1002 });
});

const languageFixture = JSON.parse(
  readFileSync(join(here, "..", "..", "contracts", "fixtures", "game-language.v1.json"), "utf8"),
);

test("game language table matches the fixture", () => {
  assert.equal(Curated.DEFAULT_GAME_LANGUAGE, languageFixture.default_language);
  assert.deepEqual(
    Curated.GAME_LANGUAGES.map(([code, name, steam, aliases]) => ({ code, name, steam, aliases })),
    languageFixture.languages,
  );
});

for (const c of languageFixture.cases) {
  test(`game language: ${c.name}`, () => {
    assert.equal(Curated.getCurrentGameLanguage(c.env), c.expect.language);
    assert.equal(Curated.getCurrentGameLanguageSteamApiName(c.env), c.expect.steam);
  });
}

test("getCurrentGameLanguage reads the process environment by default", () => {
  const saved = process.env.CURATED_LANGUAGE;
  try {
    process.env.CURATED_LANGUAGE = "es";
    assert.equal(Curated.getCurrentGameLanguage(), "es");
    assert.equal(Curated.getCurrentGameLanguageSteamApiName(), "spanish");
    delete process.env.CURATED_LANGUAGE;
    assert.equal(Curated.getCurrentGameLanguage(), "en");
  } finally {
    if (saved === undefined) delete process.env.CURATED_LANGUAGE;
    else process.env.CURATED_LANGUAGE = saved;
  }
});

test("session carries the normalized game language", async () => {
  const transport = async (_method, url) => {
    const path = new URL(url).pathname;
    if (path === "/auth/me") return { status: 200, text: '{"user_id":"u","username":"p","curated_id":"10000000000000001"}' };
    return { status: 200, text: '[{"app_id":1002,"source":"grant"}]' };
  };
  const env = {
    CURATED_LAUNCHER: "1",
    CURATED_CONTRACT_VERSION: "1",
    CURATED_APP_ID: "1002",
    CURATED_SESSION_TOKEN: "tok",
    CURATED_LANGUAGE: "it-IT",
  };
  const withLanguage = await Curated.initFromLauncher(1002, { env, transport });
  assert.equal(withLanguage.session.language, "it-IT");
  assert.equal(withLanguage.session.gameLanguage, "it");
  delete env.CURATED_LANGUAGE;
  const without = await Curated.initFromLauncher(1002, { env, transport });
  assert.equal(without.session.gameLanguage, "en");
});
