import DefaultTheme from "vitepress/theme";
import type { Theme } from "vitepress";
import VideoEmbed from "./VideoEmbed.vue";
import "./custom.css";

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    app.component("VideoEmbed", VideoEmbed);
  },
} satisfies Theme;
