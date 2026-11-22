# Announcements

**Store Presence > Announcements** (formerly Community hub) is where you post news, patch notes and
events for your game. Published announcements appear on your store page under Recent Events &
Announcements, in the News tab of your game's Community hub, and on their own shareable page.

Only the game's developer and Staff can create, edit or delete announcements.

## Admin Dashboard

The dashboard lists everything you have written:

- **Featured**: the published announcement marked as featured that has not ended yet.
- **Published announcements**: live ones, plus scheduled ones that players see once they start.
- **Drafts**: only you and Staff can see them.

Each row shows the cover image, title, date, type, the linked build (for game updates) and how many
players saw it (**Impressions:**) and opened it (**Reads:**). Use **Edit**, **View**, **Clone** and
**Delete** on each row, and the **Search** box to filter by title, subtitle or type.

### Visibility Stats

**Total Unique Impressions:** counts each player (or browser session when signed out) once per
announcement whose cover they saw. **Total Unique Views:** counts the players who opened an
announcement. **Detailed Breakdown** splits both numbers by where they happened: the store game
page, the View All list, the Community hub News tab and shared links. Launcher surfaces are listed
as coming soon. **Download .csv** exports one row per announcement with every surface.

Stats follow the store's cookie choice: players who decline analytics cookies are not counted.

## Create an announcement

Click **Create new Announcement** and pick the type that fits best, for example
**Small Update / Patch Notes**, **Major Update**, **Releasing Now**, **In-Game Event** or
**Announcement / News**. Groups with several types, such as **Game Update**, start collapsed:
click the group to show its types. The type sets the label players see. You can change it later
with **Change type** in the editor.

## Edit an announcement

The editor has five tabs. **Save** keeps your changes, **Preview** shows the store page layout.

### Description

Pick a **Language** tab (English, Spanish or Italian) to edit its text, the same way as in
[Description](description.md). English is the default and required; a dot marks the languages
that already have their own text, and empty fields in other languages show the English text.

- **Announcement title**: up to 80 characters.
- **Subtitle**: optional, up to 120 characters, shown under the title.
- **Summary**: optional, up to 180 characters, shown in lists. Left empty, one is written from the
  description.
- **Announcement description**: the same visual editor as About this game. Click an image under
  **Previously uploaded images** to add it, or upload a new one (5 MB max). Images are shared with
  Description custom media.

Game updates can link to a build: click **Select Branch** and pick a branch to link the build that
is live on it now, or **Don't associate with a build**. New announcements are not linked.

### Visibility

Start now (when you publish) or schedule a later start. End after a number of hours, days or weeks,
or at a set time: after the end the announcement stays readable but is no longer featured. Times
use your time zone; players see theirs.

- **Show on my store page**: the Recent Events & Announcements section of your store page.
- **Show in the launcher's What's New** and **Show on the launcher library game page**: saved now,
  used once the launcher shows announcements.
- **Display as featured**: pinned first on your store page until it ends. Keep it for big moments.

### Artwork

- **Cover image** (required): 800 x 450 px. Used for cards on the store page, in the View All
  list and in the Community hub.
- **Header image** (suggested): 1920 x 622 px. The banner at the top of the announcement page.
  Narrow screens crop the sides, so keep text in the center safe area.

One image is enough: the English image is used for every language. PNG, JPG or WebP, 5 MB max,
exact sizes. Spanish and Italian images are optional overrides: end the file name with the
language, for example `cover_spanish.png` or `header_italian.jpg`; no suffix means English.
Download the Curated templates (SVG with layers and safe-area guides) from the Artwork tab.

### Broadcasting

Add a **Stream link** (https) and players get a Watch the stream button on the announcement page.

### Publish

Shows the status, when it starts and the **Visibility summary** of where players will find it.
**Publish** needs an English title, an English description and a cover image. **Unpublish** turns
a published announcement back into a draft.

## What players see

On your store page, Recent Events & Announcements shows two cards (the featured one first) with
View All and "See all updates". Each announcement opens as an overlay with its own link
(`store.curated.games/app/<app id>/announcements/<id>`) that you can share, with buttons to the
newer and older announcements and the full list.
