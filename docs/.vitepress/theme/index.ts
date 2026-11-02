// Without VitePress's Inter webfont: brand.css self-hosts the store fonts.
import DefaultTheme from "vitepress/theme-without-fonts";
import type { Theme } from "vitepress";
import VideoEmbed from "./VideoEmbed.vue";
import "./brand.css";
import "./custom.css";

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component("VideoEmbed", VideoEmbed);
  },
} satisfies Theme;
