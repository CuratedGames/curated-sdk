// Runs contracts/fixtures/conformance.v1.json against CuratedSDK.js (node --test).
import { test } from "node:test";
import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import net from "node:net";

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
  assert.deepEqual(await Curated.authorizeTxn(1, 2), {
    ok: false,
    status: "error",
    error: "not_launched_by_curated",
    orderId: 1,
    transId: 2,
  });
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

function listenOverlay(handler) {
  return new Promise((resolve) => {
    const server = net.createServer((socket) => {
      let buf = "";
      socket.on("data", (chunk) => {
        buf += chunk.toString("utf8");
        const nl = buf.indexOf("\n");
        if (nl < 0) return;
        const reply = handler(buf.slice(0, nl));
        if (reply == null) return;
        socket.end(`${reply}\n`);
      });
    });
    server.listen(0, "127.0.0.1", () => resolve({ server, port: server.address().port }));
  });
}

test("authorizeTxn asks the overlay", async () => {
  const log = [];
  let seen = "";
  const overlay = await listenOverlay((line) => {
    seen = line;
    return JSON.stringify({
      v: 1,
      type: "microtxn_purchase_result",
      authorized: false,
      status: "Denied",
      order_id: 55,
      trans_id: 77,
      error_code: null,
      error_description: null,
    });
  });
  const transport = async (method, url, token, body) => {
    log.push({ line: `${method} ${url}`, token, body });
    const path = new URL(url).pathname;
    if (path === "/auth/me") return { status: 200, text: '{"user_id":"u","username":"p","curated_id":"10000000000000001"}' };
    if (path === "/library") return { status: 200, text: '[{"app_id":1002,"source":"grant"}]' };
    if (path === "/ICuratedMicroTxn/AuthorizeTxn/v1/") {
      throw new Error("client must not POST");
    }
    return { status: 500, text: "" };
  };
  const env = {
    CURATED_LAUNCHER: "1",
    CURATED_CONTRACT_VERSION: "1",
    CURATED_APP_ID: "1002",
    CURATED_SESSION_TOKEN: "tok",
  };
  const previousEndpoint = process.env.CURATED_OVERLAY_ENDPOINT;
  const previousToken = process.env.CURATED_OVERLAY_TOKEN;
  process.env.CURATED_OVERLAY_ENDPOINT = `127.0.0.1:${overlay.port}`;
  process.env.CURATED_OVERLAY_TOKEN = "overlay-token";
  try {
    assert.equal((await Curated.initFromLauncher(1002, { env, transport })).ok, true);
    const denied = await Curated.authorizeTxn(55, 77);
    assert.deepEqual(denied, { ok: true, status: "denied", orderId: 55, transId: 77 });
    assert.equal(
      seen,
      '{"v":1,"token":"overlay-token","type":"microtxn_purchase","app_id":1002,"order_id":55,"trans_id":77}',
    );
    assert.equal(log.some((entry) => String(entry.line).includes("AuthorizeTxn")), false);

    process.env.CURATED_OVERLAY_ENDPOINT = "";
    const missing = await Curated.authorizeTxn(55, 77);
    assert.equal(missing.status, "error");
    assert.equal(missing.ok, false);
    assert.equal(missing.errorDescription, "Purchases require the Curated launcher overlay.");
    assert.equal(log.some((entry) => String(entry.line).includes("AuthorizeTxn")), false);
  } finally {
    overlay.server.close();
    if (previousEndpoint === undefined) delete process.env.CURATED_OVERLAY_ENDPOINT;
    else process.env.CURATED_OVERLAY_ENDPOINT = previousEndpoint;
    if (previousToken === undefined) delete process.env.CURATED_OVERLAY_TOKEN;
    else process.env.CURATED_OVERLAY_TOKEN = previousToken;
  }
});

test("authorizeTxn maps an overlay error result", async () => {
  const overlay = await listenOverlay(() =>
    JSON.stringify({
      v: 1,
      type: "microtxn_purchase_result",
      authorized: false,
      status: "Error",
      order_id: 55,
      trans_id: 77,
      error_code: 7,
      error_description: "Order is not pending",
    }),
  );
  const transport = async (_method, url) => {
    const path = new URL(url).pathname;
    if (path === "/auth/me") return { status: 200, text: '{"user_id":"u","username":"p","curated_id":"10000000000000001"}' };
    if (path === "/library") return { status: 200, text: '[{"app_id":1002,"source":"grant"}]' };
    return { status: 500, text: "" };
  };
  const env = {
    CURATED_LAUNCHER: "1",
    CURATED_CONTRACT_VERSION: "1",
    CURATED_APP_ID: "1002",
    CURATED_SESSION_TOKEN: "tok",
  };
  const previousEndpoint = process.env.CURATED_OVERLAY_ENDPOINT;
  const previousToken = process.env.CURATED_OVERLAY_TOKEN;
  process.env.CURATED_OVERLAY_ENDPOINT = `127.0.0.1:${overlay.port}`;
  process.env.CURATED_OVERLAY_TOKEN = "overlay-token";
  try {
    assert.equal((await Curated.initFromLauncher(1002, { env, transport })).ok, true);
    const failed = await Curated.authorizeTxn(55, 77);
    assert.deepEqual(failed, {
      ok: false,
      status: "error",
      error: "hub_error",
      orderId: 55,
      transId: 77,
      errorCode: 7,
      errorDescription: "Order is not pending",
    });
  } finally {
    overlay.server.close();
    if (previousEndpoint === undefined) delete process.env.CURATED_OVERLAY_ENDPOINT;
    else process.env.CURATED_OVERLAY_ENDPOINT = previousEndpoint;
    if (previousToken === undefined) delete process.env.CURATED_OVERLAY_TOKEN;
    else process.env.CURATED_OVERLAY_TOKEN = previousToken;
  }
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

// Script calls shown on docs.curated.games. The docs import the #region blocks, so every
// snippet there runs here first.
test("docs snippets: script calls after the launch check", async () => {
  const log = [];
  const transport = async (method, url, _token, body) => {
    log.push(`${method} ${new URL(url).pathname}`);
    const path = new URL(url).pathname;
    if (path === "/auth/me") return { status: 200, text: '{"user_id":"u","username":"p","curated_id":"10000000000000001"}' };
    if (path === "/library") return { status: 200, text: '[{"app_id":1002,"source":"grant"}]' };
    if (path.endsWith("/unlock")) return { status: 200, text: '{"newly_unlocked":true}' };
    if (path.endsWith("/stats/progress")) return { status: 200, text: '{"stats":[]}' };
    if (path.includes("/stats/")) return { status: 200, text: '{"stat":{"api_name":"CardsCreated","value":10},"previous_value":0}' };
    return { status: 500, text: "" };
  };
  const env = { CURATED_LAUNCHER: "1", CURATED_CONTRACT_VERSION: "1", CURATED_APP_ID: "1002", CURATED_SESSION_TOKEN: "tok" };
  const ready = await Curated.initFromLauncher(1002, { env, transport });
  assert.equal(ready.ok, true);

  // #region script-calls
  const unlocked = await Curated.unlockAchievement("BOSS_1"); // { ok, newlyUnlocked }
  await Curated.refreshStats(); // { ok, stats }
  await Curated.setStat("CardsCreated", 10);
  await Curated.incrementStat("CardsCreated", 1);
  // #endregion script-calls
  assert.deepEqual(unlocked, { ok: true, newlyUnlocked: true });

  // #region session
  const { username, curatedId, gameLanguage } = Curated.session;
  // #endregion session
  assert.equal(username, "p");
  assert.equal(curatedId, "10000000000000001");
  assert.equal(gameLanguage, "en");

  const overlay = await listenOverlay(() =>
    JSON.stringify({
      v: 1,
      type: "microtxn_purchase_result",
      authorized: true,
      status: "Authorized",
      order_id: 938473,
      trans_id: 374839,
      error_code: null,
      error_description: null,
    }),
  );
  const previousEndpoint = process.env.CURATED_OVERLAY_ENDPOINT;
  const previousToken = process.env.CURATED_OVERLAY_TOKEN;
  process.env.CURATED_OVERLAY_ENDPOINT = `127.0.0.1:${overlay.port}`;
  process.env.CURATED_OVERLAY_TOKEN = "overlay-token";
  try {
    // #region purchase
    // orderId and transId come from your game server, which created the order with InitTxn.
    // This asks the Curated launcher overlay. Outside the launcher the status is "error".
    // The client does not POST.
    const purchase = await Curated.authorizeTxn(938473, 374839);
    if (purchase.status === "authorized") {
      // Tell your game server; it calls FinalizeTxn with the publisher key.
    }
    // #endregion purchase
    assert.equal(purchase.status, "authorized");
    assert.equal(log.includes("POST /ICuratedMicroTxn/AuthorizeTxn/v1/"), false);
  } finally {
    overlay.server.close();
    if (previousEndpoint === undefined) delete process.env.CURATED_OVERLAY_ENDPOINT;
    else process.env.CURATED_OVERLAY_ENDPOINT = previousEndpoint;
    if (previousToken === undefined) delete process.env.CURATED_OVERLAY_TOKEN;
    else process.env.CURATED_OVERLAY_TOKEN = previousToken;
  }
  assert.ok(log.includes("POST /games/app/1002/stats/CardsCreated/increment"));
});
