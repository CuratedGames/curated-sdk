//=============================================================================
// CuratedSDK.js
// Curated launcher sign-in and ownership check for RPG Maker MZ / MV.
//=============================================================================
/*:
 * @target MZ
 * @plugindesc v1.1.0 Curated launcher sign-in, ownership check, and achievements.
 * @author Curated
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
 * MV plugin command:        Curated unlockAchievement win_level_1
 *
 * Results: Curated.session holds { userId, username, language,
 * entitlement: { entitled, source, acquiredAt } } after a successful check.
 */

(function (root) {
  "use strict";

  const CONTRACT_VERSION = 1;
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
    if (!isPositiveInteger(version) || Number(version) > CONTRACT_VERSION) {
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

  /** HTTP GET/POST via Node https (NW.js) when available, else fetch. Resolves { status, text }. */
  function defaultTransport(method, url, token) {
    const headers = { Authorization: `Bearer ${token}`, Accept: "application/json" };
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
        req.end();
      });
    }
    return fetch(url, { method, headers }).then(async (res) => ({
      status: res.status,
      text: await res.text(),
    }));
  }

  async function callApi(context, transport, method, path) {
    let res;
    try {
      res = await transport(method, `${context.apiBase}${path}`, context.token);
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

  const Curated = {
    CONTRACT_VERSION,
    DEFAULT_API_BASE,
    ERRORS,
    parseLaunchEnv,
    session: null,
    _context: null,
    _transport: defaultTransport,

    /**
     * Read launch variables, verify the session, and confirm ownership.
     * Resolves { ok: true, session } or { ok: false, error } with a contract error code.
     */
    async initFromLauncher(appId, options = {}) {
      const env = options.env || (typeof process !== "undefined" ? process.env : {});
      const transport = options.transport || defaultTransport;
      this.session = null;
      this._context = null;
      const parsed = parseLaunchEnv(env, appId);
      if (!parsed.ok) return parsed;
      const context = parsed.context;

      const me = await callApi(context, transport, "GET", "/auth/me");
      if (!me.ok) return me;
      if (!me.body || typeof me.body.user_id !== "string") return { ok: false, error: ERRORS.HUB };

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
        username: String(me.body.username || ""),
        language: context.language,
        entitlement,
      };
      return { ok: true, session: this.session };
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
      const res = await callApi(this._context, this._transport, "POST", path);
      if (!res.ok) return res;
      return { ok: true, newlyUnlocked: Boolean(res.body && res.body.newly_unlocked) };
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
