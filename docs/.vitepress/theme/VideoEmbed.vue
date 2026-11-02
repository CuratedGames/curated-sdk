<script setup lang="ts">
// Video in a docs page without committing the file:
//   <VideoEmbed src="https://www.youtube.com/watch?v=VIDEO_ID" title="Uploading a build" />
//   <VideoEmbed src="https://cdn.curated.games/docs/upload.mp4" title="Uploading a build" />
// YouTube links (watch, youtu.be, shorts, embed) use the privacy-enhanced player. Anything
// else is played with <video>, so use an MP4 or WebM URL.
import { computed } from "vue";

const props = defineProps<{ src: string; title: string; poster?: string }>();

const youtubeId = computed(() => {
  try {
    const url = new URL(props.src);
    const host = url.hostname.replace(/^www\./, "");
    if (host === "youtu.be") return url.pathname.slice(1);
    if (host === "youtube.com" || host === "youtube-nocookie.com" || host === "m.youtube.com") {
      if (url.searchParams.get("v")) return url.searchParams.get("v");
      const m = url.pathname.match(/^\/(embed|shorts)\/([\w-]+)/);
      if (m) return m[2];
    }
  } catch {
    return null;
  }
  return null;
});
</script>

<template>
  <figure class="video-embed">
    <div class="video-embed-frame">
      <iframe
        v-if="youtubeId"
        :src="`https://www.youtube-nocookie.com/embed/${youtubeId}`"
        :title="title"
        loading="lazy"
        allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
        allowfullscreen
      />
      <video v-else :src="src" :poster="poster" :title="title" controls preload="metadata" playsinline />
    </div>
    <figcaption>{{ title }}</figcaption>
  </figure>
</template>

<style scoped>
.video-embed { margin: 16px 0; }
.video-embed-frame { position: relative; aspect-ratio: 16 / 9; border-radius: 8px; overflow: hidden; background: #000; }
.video-embed-frame iframe, .video-embed-frame video { position: absolute; inset: 0; width: 100%; height: 100%; border: 0; }
figcaption { margin-top: 8px; font-size: 14px; color: var(--vp-c-text-2); }
</style>
