import { readFileSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vitepress";

// Written by scripts/docs_gen.py before every build (latest release tag, falls back to the
// Curated.Sdk.csproj version). Docs never hardcode an SDK version: write %SDK_VERSION% or
// %SDK_TAG% and it is replaced here, in prose and in code blocks alike.
const generatedPath = fileURLToPath(new URL("./generated.json", import.meta.url));
const generated = existsSync(generatedPath)
  ? JSON.parse(readFileSync(generatedPath, "utf8"))
  : { version: "dev", tag: "main", versionSource: "missing" };

// Link targets are percent-encoded by markdown-it before this runs, so match both spellings.
const replacements: Record<string, string> = {
  "%SDK_VERSION%": generated.version,
  "%SDK_TAG%": generated.tag,
  "%25SDK_VERSION%25": generated.version,
  "%25SDK_TAG%25": generated.tag,
};

function replaceVersion(text: string): string {
  let out = text;
  for (const [key, value] of Object.entries(replacements)) out = out.split(key).join(value);
  return out;
}

const repo = "https://github.com/CuratedGames/curated-sdk";

export default defineConfig({
  title: "Curated Docs",
  description: "Integrate games with Curated: SDK guides, Content Builder, launch contract, and the developer admin.",
  lang: "en-US",
  base: "/",
  cleanUrls: true,
  lastUpdated: true,
  appearance: "dark",
  srcExclude: ["README.md"],
  head: [["link", { rel: "icon", href: "/favicon.svg", type: "image/svg+xml" }]],
  markdown: {
    config(md) {
      md.core.ruler.push("curated_sdk_version", (state) => {
        const walk = (tokens: typeof state.tokens) => {
          for (const token of tokens) {
            if (token.content) token.content = replaceVersion(token.content);
            if (token.attrs) token.attrs = token.attrs.map(([k, v]) => [k, replaceVersion(v)]);
            if (token.children) walk(token.children);
          }
        };
        walk(state.tokens);
      });
    },
  },
  themeConfig: {
    siteTitle: "Curated Docs",
    logo: "/favicon.svg",
    nav: [
      { text: "SDK", link: "/guide/" },
      { text: "Content Builder", link: "/guide/content-builder" },
      { text: "Admin", link: "/admin/" },
      { text: "API reference", link: "/reference/" },
      { text: `v${generated.version}`, items: [
        { text: "Changelog", link: "/changelog" },
        { text: "Releases", link: `${repo}/releases` },
      ] },
    ],
    sidebar: {
      "/guide/": [
        {
          text: "Start here",
          items: [
            { text: "Overview", link: "/guide/" },
            { text: "Launch contract", link: "/guide/launch-contract" },
            { text: "Testing outside the launcher", link: "/guide/testing" },
          ],
        },
        {
          text: "Engines",
          items: [
            { text: "Unity", link: "/guide/unity" },
            { text: ".NET (C#)", link: "/guide/dotnet" },
            { text: "Godot 4", link: "/guide/godot" },
            { text: "Unreal Engine 5", link: "/guide/unreal" },
            { text: "RPG Maker MZ / MV", link: "/guide/rpgmaker" },
          ],
        },
        {
          text: "Features",
          items: [
            { text: "Achievements and stats", link: "/guide/achievements-stats" },
            { text: "Game language", link: "/guide/game-language" },
            { text: "Cloud Save, friends, presence", link: "/guide/cloud-friends-presence" },
            { text: "In-game purchases", link: "/guide/purchases" },
            { text: "OpenID Connect", link: "/guide/openid" },
            { text: "Migrating from Steam", link: "/guide/migrating-from-steam" },
          ],
        },
        {
          text: "Shipping builds",
          items: [{ text: "Content Builder", link: "/guide/content-builder" }],
        },
      ],
      "/admin/": [
        {
          text: "Developer admin",
          items: [
            { text: "Overview", link: "/admin/" },
            { text: "Release Progress", link: "/admin/release-progress" },
          ],
        },
        {
          text: "Store Presence",
          items: [
            { text: "Basic Info", link: "/admin/store-presence/basic-info" },
            { text: "Description", link: "/admin/store-presence/description" },
            { text: "Graphical Assets", link: "/admin/store-presence/graphical-assets" },
            { text: "Content Survey", link: "/admin/store-presence/content-survey" },
            { text: "Pricing", link: "/admin/store-presence/pricing" },
            { text: "Publish and player reviews", link: "/admin/store-presence/publish" },
          ],
        },
        {
          text: "Game Build",
          items: [
            { text: "Builds", link: "/admin/game-build/builds" },
            { text: "Depots", link: "/admin/game-build/depots" },
            { text: "Packages and product keys", link: "/admin/game-build/packages" },
            { text: "Installation", link: "/admin/game-build/installation" },
            { text: "EULA", link: "/admin/game-build/eula" },
            { text: "Achievements and Stats", link: "/admin/game-build/achievements-stats" },
          ],
        },
        {
          text: "After submission",
          items: [
            { text: "Release", link: "/admin/release" },
            { text: "Resales and paid reviews", link: "/admin/visibility-sales" },
            { text: "Marketing", link: "/admin/marketing" },
            { text: "Demo", link: "/admin/demo" },
            { text: "More tools", link: "/admin/more-tools" },
            { text: "Publisher Web API key", link: "/admin/web-api" },
          ],
        },
      ],
      "/reference/": [
        {
          text: "API reference",
          items: [
            { text: "Overview", link: "/reference/" },
            { text: ".NET (Curated.Sdk)", link: "/reference/dotnet" },
            { text: "Unity", link: "/reference/unity" },
            { text: "Godot 4", link: "/reference/godot" },
            { text: "Unreal Engine 5", link: "/reference/unreal" },
            { text: "RPG Maker", link: "/reference/rpgmaker" },
            { text: "Content Builder CLI", link: "/reference/content-builder" },
          ],
        },
      ],
    },
    socialLinks: [{ icon: "github", link: repo }],
    editLink: {
      pattern: `${repo}/edit/main/docs/:path`,
      text: "Edit this page on GitHub",
    },
    search: { provider: "local" },
    outline: { level: [2, 3] },
    footer: {
      message: "Curated SDK License. Source-available, not open source.",
      copyright: "Curated and Curated Games are trademarks of Margherita Roncone, licensed to Fatum Games SL.",
    },
  },
});
