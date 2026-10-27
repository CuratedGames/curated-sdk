//=============================================================================
// CuratedSDK.js
// Curated launcher sign-in and ownership check for RPG Maker MZ / MV.
//=============================================================================
/*:
 * @target MZ
 * @plugindesc v1.5.1 Curated launcher sign-in, ownership check, achievements, and stats.
 * @author Fatum Games SL
 * @url https://github.com/CuratedGames/curated-sdk
 *
 * @param appId
 * @text Curated app id
 * @desc Your game's app id on Curated (from the developer admin).
 * @type number
 * @min 1
 * @default 0
 *
 * @param quitIfNotEntitled
 * @text Quit when not owned
 * @desc Close the game when the player does not own it on Curated or was not launched by Curated.
 * @type boolean
 * @default true
 *
 * @param blockedMessage
 * @text Blocked message
 * @desc Shown before quitting when the launch check fails.
 * @type string
 * @default Please start this game from the Curated launcher.
 *
 * @command unlockAchievement
 * @text Unlock achievement
 * @desc Unlock a Curated achievement by its API name.
 *
 * @arg name
 * @text API name
 * @type string
 *
 * @help CuratedSDK.js
 *
 * Implements the Curated launch contract v1 (contracts/launch-contract.md).
 * When the Curated launcher starts the game it passes the player session in
 * environment variables. This plugin verifies the session and ownership
 * before the title screen appears.
 *
 * Test play from the editor skips the check, so you can keep developing
 * without the launcher.
 *
 * Plugin command (MZ): Unlock achievement.
 * Script call (MZ and MV):  Curated.unlockAchievement("win_level_1");
 *                           Curated.setStat("CardsCreated", 10);
 *                           Curated.incrementStat("CardsCreated", 1);
 * MV plugin command:        Curated unlockAchievement win_level_1
 *
 * Results: Curated.session holds { userId, curatedId, username, language, gameLanguage,
 * entitlement: { entitled, source, acquiredAt } } after a successful check.
 *
 * Game language: Curated.getCurrentGameLanguage() returns the language the
 * player selected in the Curated launcher as a Curated code ("en", "es",
 * "it", ...), or "en" outside the launcher. Migrating from Steam: use
 * Curated.getCurrentGameLanguageSteamApiName() for Steam client language names.
 */

(function (root) {
  "use strict";

  const CONTRACT_VERSION = 1;
  /** Oldest contract version still accepted. Newer launcher versions stay OK. */
  const MIN_COMPATIBLE_CONTRACT_VERSION = 1;
  const DEFAULT_API_BASE = "https://api.curated.games";
  const ERRORS = Object.freeze({
    NOT_LAUNCHED: "not_launched_by_curated",
    UNSUPPORTED_VERSION: "unsupported_contract_version",
    APP_MISMATCH: "app_mismatch",
    SESSION_INVALID: "session_invalid",
    NOT_ENTITLED: "not_entitled",
    NETWORK: "network_error",
    HUB: "hub_error",
  });

  const DEFAULT_GAME_LANGUAGE = "en";
  // Curated game languages: [code, English name, Steam client API name (Migrating from Steam), aliases].
  // Same table as contracts/fixtures/game-language.v1.json.
  const GAME_LANGUAGES = Object.freeze([
    ["ar", "Arabic", "arabic", []],
    ["bg", "Bulgarian", "bulgarian", []],
    ["zh-cn", "Chinese (Simplified)", "schinese", ["zh", "zh-hans"]],
    ["zh-tw", "Chinese (Traditional)", "tchinese", ["zh-hant"]],
    ["cs", "Czech", "czech", []],
    ["da", "Danish", "danish", []],
    ["nl", "Dutch", "dutch", []],
    ["en", "English", "english", []],
    ["fi", "Finnish", "finnish", []],
    ["fr", "French", "french", []],
    ["de", "German", "german", []],
    ["el", "Greek", "greek", []],
    ["hu", "Hungarian", "hungarian", []],
    ["id", "Indonesian", "indonesian", []],
    ["it", "Italian", "italian", []],
    ["ja", "Japanese", "japanese", []],
    ["ko", "Korean", "koreana", []],
    ["ms", "Malay", "malay", []],
    ["no", "Norwegian", "norwegian", ["nb"]],
    ["pl", "Polish", "polish", []],
    ["pt", "Portuguese", "portuguese", []],
    ["pt-br", "Portuguese-Brazil", "brazilian", []],
    ["ro", "Romanian", "romanian", []],
    ["ru", "Russian", "russian", []],
    ["es-419", "Spanish-Latin America", "latam", []],
    ["es", "Spanish-Spain", "spanish", []],
    ["sv", "Swedish", "swedish", []],
    ["th", "Thai", "thai", []],
    ["tr", "Turkish", "turkish", []],
    ["uk", "Ukrainian", "ukrainian", []],
    ["vi", "Vietnamese", "vietnamese", []],
  ]);

  /** Canonical Curated code for a code, alias, BCP-47 tag or Steam client API name, or null. */
  function normalizeGameLanguage(raw) {
    if (typeof raw !== "string") return null;
    const key = raw.trim().toLowerCase().replace(/_/g, "-");
    if (!key) return null;
    const byCode = (k) => GAME_LANGUAGES.find((l) => l[0] === k || l[3].includes(k));
    const hit = byCode(key) || GAME_LANGUAGES.find((l) => l[2] === key);
    if (hit) return hit[0];
    const dash = key.indexOf("-");
    const primary = dash > 0 ? byCode(key.slice(0, dash)) : null;
    return primary ? primary[0] : null;
  }

  /** Migrating from Steam: Steam client API language name for a Curated code (or any accepted value), or null. */
  function steamApiLanguageName(language) {
    const code = normalizeGameLanguage(language);
    const hit = GAME_LANGUAGES.find((l) => l[0] === code);
    return hit ? hit[2] : null;
  }

  function gameLanguageFromEnv(env) {
    return normalizeGameLanguage(readVar(env, "CURATED_LANGUAGE")) || DEFAULT_GAME_LANGUAGE;
  }

  function processEnv() {
    return typeof process !== "undefined" && process.env ? process.env : {};
  }

  function readVar(env, name) {
    const raw = env ? env[name] : undefined;
    if (typeof raw !== "string") return null;
    const value = raw.trim();
    return value.length ? value : null;
  }

  function isPositiveInteger(text) {
    return typeof text === "string" && /^[0-9]+$/.test(text) && Number(text) > 0;
  }

  /** Validate launch variables. Returns { ok, error } or { ok: true, context }. */
  function parseLaunchEnv(env, expectedAppId) {
    const token = readVar(env, "CURATED_SESSION_TOKEN");
    if (readVar(env, "CURATED_LAUNCHER") !== "1" || !token) {
      return { ok: false, error: ERRORS.NOT_LAUNCHED };
    }
    const version = readVar(env, "CURATED_CONTRACT_VERSION");
    if (!isPositiveInteger(version) || Number(version) < MIN_COMPATIBLE_CONTRACT_VERSION) {
      return { ok: false, error: ERRORS.UNSUPPORTED_VERSION };
    }
    const appId = readVar(env, "CURATED_APP_ID");
    if (!isPositiveInteger(appId) || Number(appId) !== Number(expectedAppId)) {
      return { ok: false, error: ERRORS.APP_MISMATCH };
    }
    let apiBase = DEFAULT_API_BASE;
    const apiRaw = readVar(env, "CURATED_API_BASE");
    if (apiRaw) {
      const trimmed = apiRaw.replace(/\/+$/, "");
      if (!/^https?:\/\/[^\s/]+/i.test(trimmed)) {
        return { ok: false, error: ERRORS.NOT_LAUNCHED };
      }
      apiBase = trimmed;
    }
    return {
      ok: true,
      context: {
        contractVersion: Number(version),
        appId: Number(appId),
        token,
        apiBase,
        userIdHint: readVar(env, "CURATED_USER_ID"),
        usernameHint: readVar(env, "CURATED_USERNAME"),
        language: readVar(env, "CURATED_LANGUAGE"),
      },
    };
  }

  /** HTTP GET/POST/PUT via Node https (NW.js) when available, else fetch. Resolves { status, text }. */
  function defaultTransport(method, url, token, body) {
    const headers = { Authorization: `Bearer ${token}`, Accept: "application/json" };
    const payload = method === "GET" || body == null ? null : typeof body === "string" ? body : JSON.stringify(body);
    if (payload != null) headers["Content-Type"] = "application/json";
    if (typeof require === "function") {
      const https = url.startsWith("https:") ? require("https") : require("http");
      return new Promise((resolve, reject) => {
        const req = https.request(url, { method, headers }, (res) => {
          let text = "";
          res.setEncoding("utf8");
          res.on("data", (chunk) => (text += chunk));
          res.on("end", () => resolve({ status: res.statusCode, text }));
        });
        req.on("error", reject);
        req.setTimeout(15000, () => req.destroy(new Error("timeout")));
        if (payload != null) req.write(payload);
        req.end();
      });
    }
    return fetch(url, { method, headers, body: payload == null ? undefined : payload }).then(async (res) => ({
      status: res.status,
      text: await res.text(),
    }));
  }

  async function callApi(context, transport, method, path, body) {
    let res;
    try {
      res = await transport(method, `${context.apiBase}${path}`, context.token, body);
    } catch (_) {
      return { ok: false, error: ERRORS.NETWORK };
    }
    if (res.status === 401) return { ok: false, error: ERRORS.SESSION_INVALID };
    if (res.status < 200 || res.status >= 300) return { ok: false, error: ERRORS.HUB };
    try {
      return { ok: true, body: res.text ? JSON.parse(res.text) : null };
    } catch (_) {
      return { ok: false, error: ERRORS.HUB };
    }
  }

  function findEntitlement(items, appId) {
    if (!Array.isArray(items)) return null;
    const hit = items.find((item) => item && Number(item.app_id) === Number(appId));
    if (!hit) return { entitled: false, source: null, acquiredAt: null };
    return { entitled: true, source: hit.source || null, acquiredAt: hit.acquired_at || null };
  }

  /** Notify the Curated launcher overlay (NW.js / Node only; no-op in browser). */
  function notifyOverlayAchievement(body, fallbackAppId) {
    try {
      const env = processEnv();
      const endpoint = String((env && env.CURATED_OVERLAY_ENDPOINT) || "").trim();
      const token = String((env && env.CURATED_OVERLAY_TOKEN) || "").trim();
      if (!endpoint || !token || typeof require !== "function") return;
      const colon = endpoint.lastIndexOf(":");
      if (colon <= 0) return;
      const host = endpoint.slice(0, colon).trim() || "127.0.0.1";
      const port = Number(endpoint.slice(colon + 1));
      if (!port) return;
      const net = require("net");
      const payload = {
        v: 1,
        token,
        type: "achievement_unlocked",
        app_id: Number((body && body.app_id) || fallbackAppId) || 0,
        api_name: String((body && body.api_name) || ""),
        display_name: String((body && body.display_name) || (body && body.api_name) || ""),
        description: String((body && body.description) || ""),
      };
      if (body && body.icon_achieved_uri) payload.icon_url = String(body.icon_achieved_uri);
      if (body && body.global_unlock_percent != null) {
        payload.global_unlock_percent = Number(body.global_unlock_percent);
      }
      const socket = net.connect({ host, port }, () => {
        socket.end(JSON.stringify(payload) + "\n");
      });
      socket.setTimeout(750);
      socket.on("error", () => {
        try {
          socket.destroy();
        } catch (_) {}
      });
      socket.on("timeout", () => {
        try {
          socket.destroy();
        } catch (_) {}
      });
    } catch (_) {
      /* overlay optional */
    }
  }

  const Curated = {
    CONTRACT_VERSION,
    MIN_COMPATIBLE_CONTRACT_VERSION,
    DEFAULT_API_BASE,
    ERRORS,
    parseLaunchEnv,
    DEFAULT_GAME_LANGUAGE,
    GAME_LANGUAGES,
    normalizeGameLanguage,
    steamApiLanguageName,
    session: null,
    _context: null,
    _transport: defaultTransport,

    /**
     * Read launch variables, verify the session, and confirm ownership.
     * Resolves { ok: true, session } or { ok: false, error } with a contract error code.
     */
    async initFromLauncher(appId, options = {}) {
      const env = options.env || processEnv();
      const transport = options.transport || defaultTransport;
      this.session = null;
      this._context = null;
      const parsed = parseLaunchEnv(env, appId);
      if (!parsed.ok) return parsed;
      const context = parsed.context;

      const me = await callApi(context, transport, "GET", "/auth/me");
      if (!me.ok) return me;
      if (!me.body || typeof me.body.user_id !== "string") return { ok: false, error: ERRORS.HUB };
      const curatedId = String(me.body.curated_id || "").trim();
      if (!curatedId) return { ok: false, error: ERRORS.HUB };

      const lib = await callApi(context, transport, "GET", "/library");
      if (!lib.ok) return lib;
      const entitlement = findEntitlement(lib.body, context.appId);
      if (!entitlement) return { ok: false, error: ERRORS.HUB };
      if (!entitlement.entitled) return { ok: false, error: ERRORS.NOT_ENTITLED };

      this._context = context;
      this._transport = transport;
      this.session = {
        appId: context.appId,
        apiBase: context.apiBase,
        userId: me.body.user_id,
        curatedId,
        username: String(me.body.username || ""),
        language: context.language,
        gameLanguage: normalizeGameLanguage(context.language) || DEFAULT_GAME_LANGUAGE,
        entitlement,
      };
      return { ok: true, session: this.session };
    },

    /**
     * Game language the player selected in the Curated launcher, as a lowercase Curated code
     * ("en", "es", "it", "pt-br", ...). It is the launcher's client language, the same one used
     * to pick language depots. Returns "en" when the game was not started by the launcher, no
     * language was sent, or it is not a Curated game language. Works before initFromLauncher.
     * `env` is for tests.
     */
    getCurrentGameLanguage(env) {
      return gameLanguageFromEnv(env || processEnv());
    },

    /** Migrating from Steam: getCurrentGameLanguage() as a Steam client language API name ("english", "spanish", "italian", ...). */
    getCurrentGameLanguageSteamApiName(env) {
      return steamApiLanguageName(this.getCurrentGameLanguage(env));
    },

    /** Re-check ownership during a long session. Resolves { ok, entitlement } or { ok: false, error }. */
    async checkEntitlement() {
      if (!this._context) return { ok: false, error: ERRORS.NOT_LAUNCHED };
      const lib = await callApi(this._context, this._transport, "GET", "/library");
      if (!lib.ok) return lib;
      const entitlement = findEntitlement(lib.body, this._context.appId);
      return entitlement ? { ok: true, entitlement } : { ok: false, error: ERRORS.HUB };
    },

    /** Unlock an achievement by API name (idempotent). Resolves { ok, newlyUnlocked } or { ok: false, error }. */
    async unlockAchievement(name) {
      if (!this._context) return { ok: false, error: ERRORS.NOT_LAUNCHED };
      const apiName = String(name || "").trim();
      if (!apiName) return { ok: false, error: ERRORS.HUB };
      const path = `/games/app/${this._context.appId}/achievements/${encodeURIComponent(apiName)}/unlock`;
      const res = await callApi(this._context, this._transport, "POST", path, {});
      if (!res.ok) return res;
      const newlyUnlocked = Boolean(res.body && res.body.newly_unlocked);
      if (newlyUnlocked) notifyOverlayAchievement(res.body, this._context.appId);
      return { ok: true, newlyUnlocked };
    },

    /** Load stat definitions and current player values. Resolves { ok, stats } or { ok: false, error }. */
    async refreshStats() {
      if (!this._context) return { ok: false, error: ERRORS.NOT_LAUNCHED };
      const path = `/games/app/${this._context.appId}/stats/progress`;
      const res = await callApi(this._context, this._transport, "GET", path);
      if (!res.ok) return res;
      const stats = res.body && Array.isArray(res.body.stats) ? res.body.stats : [];
      return { ok: true, stats };
    },

    /** SetStat absolute value. Resolves { ok, stat, previousValue } or { ok: false, error }. */
    async setStat(name, value) {
      if (!this._context) return { ok: false, error: ERRORS.NOT_LAUNCHED };
      const apiName = String(name || "").trim();
      if (!apiName) return { ok: false, error: ERRORS.HUB };
      const path = `/games/app/${this._context.appId}/stats/${encodeURIComponent(apiName)}`;
      const res = await callApi(this._context, this._transport, "PUT", path, { value: Number(value) });
      if (!res.ok) return res;
      return {
        ok: true,
        stat: (res.body && res.body.stat) || null,
        previousValue: res.body ? res.body.previous_value : null,
      };
    },

    /** Increment a player stat. Resolves { ok, stat, previousValue } or { ok: false, error }. */
    async incrementStat(name, delta = 1) {
      if (!this._context) return { ok: false, error: ERRORS.NOT_LAUNCHED };
      const apiName = String(name || "").trim();
      if (!apiName) return { ok: false, error: ERRORS.HUB };
      const path = `/games/app/${this._context.appId}/stats/${encodeURIComponent(apiName)}/increment`;
      const res = await callApi(this._context, this._transport, "POST", path, { delta: Number(delta) });
      if (!res.ok) return res;
      return {
        ok: true,
        stat: (res.body && res.body.stat) || null,
        previousValue: res.body ? res.body.previous_value : null,
      };
    },

    /**
     * Mint a short-lived OpenID Connect ID token. Never log it.
     * Resolves { ok, idToken } or { ok: false, error }.
     */
    async getOpenIdIdToken() {
      if (!this._context) return { ok: false, error: ERRORS.NOT_LAUNCHED };
      const res = await callApi(this._context, this._transport, "POST", "/auth/openid/id-token", {
        app_id: this._context.appId,
      });
      if (!res.ok) return res;
      const idToken = res.body && typeof res.body.id_token === "string" ? res.body.id_token.trim() : "";
      if (!idToken) return { ok: false, error: ERRORS.HUB };
      return { ok: true, idToken };
    },
  };

  if (typeof module === "object" && module.exports && typeof PluginManager === "undefined") {
    module.exports = Curated;
    return;
  }
  root.Curated = Curated;

  //---------------------------------------------------------------------------
  // RPG Maker wiring
  //---------------------------------------------------------------------------
  const params = PluginManager.parameters("CuratedSDK");
  const appId = Number(params.appId || 0);
  const quitIfNotEntitled = String(params.quitIfNotEntitled || "true") === "true";
  const blockedMessage = String(params.blockedMessage || "Please start this game from the Curated launcher.");
  const isTestPlay = typeof Utils !== "undefined" && Utils.isOptionValid && Utils.isOptionValid("test");
  let launchDone = isTestPlay || appId <= 0;

  if (!launchDone) {
    Curated.initFromLauncher(appId).then((result) => {
      if (!result.ok) {
        console.warn(`[Curated] launch check failed: ${result.error}`);
        if (quitIfNotEntitled) {
          window.alert(blockedMessage);
          SceneManager.exit();
          return;
        }
      }
      launchDone = true;
    });
  } else if (appId <= 0) {
    console.warn("[Curated] Set the Curated app id in the CuratedSDK plugin parameters.");
  }

  const bootIsReady = Scene_Boot.prototype.isReady;
  Scene_Boot.prototype.isReady = function () {
    return bootIsReady.call(this) && launchDone;
  };

  if (PluginManager.registerCommand) {
    PluginManager.registerCommand("CuratedSDK", "unlockAchievement", (args) => {
      Curated.unlockAchievement(args.name);
    });
  }

  if (typeof Game_Interpreter !== "undefined") {
    const pluginCommand = Game_Interpreter.prototype.pluginCommand;
    Game_Interpreter.prototype.pluginCommand = function (command, args) {
      if (pluginCommand) pluginCommand.call(this, command, args);
      if (command === "Curated" && args[0] === "unlockAchievement") {
        Curated.unlockAchievement(args[1]);
      }
    };
  }
})(typeof globalThis !== "undefined" ? globalThis : this);
