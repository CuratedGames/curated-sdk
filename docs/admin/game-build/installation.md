# Installation

**Game Build > Installation** tells the Curated launcher where to install the game, how to start
it, and which icons to use.

If the app is linked to Steam, **Re-import install settings and client icons from Steam** copies
the install folder, launch options, and icons from Steam.

## Install Folder

The folder the game installs into, under `games/common/` in the player's library. Leave it empty
to use the game name. Letters, numbers, spaces, dots, dashes, and underscores work on every
Windows PC. Click **Update Install Folder** to save. Players get the change with the next
download.

## Launch Options

You need at least one launch option, so the launcher knows what to run. Click **Add new launch
option** and fill in:

- **Executable** (required): the file to run, with its path relative to the install folder if it
  is in a subfolder.
- **Arguments**: passed to the executable on launch.
- **Working Dir**: only if the game must start from a folder other than the install folder.
- **Description**: shown to players only when more than one option exists for their OS. You can
  write it per language.
- **Operating System**: the OS this option is for.
- **Beta Branch**: offer the option on every channel, or only to players on the beta channel.

Click **Update** to save. Each option has **Edit** and **Delete**.

Players can add their own launch options in the launcher, and the SDK passes them through as
normal arguments. See [Launch contract](/guide/launch-contract#command-line-arguments).

## Client images and Icons

- **Shortcut Icon** (required): used for desktop and Start menu shortcuts. ICO or PNG; an ICO
  must contain a 256x256 image. You can tick **Convert shortcut icon to app icon at upload time**.
- **App Icon** (required): compact lists in the launcher and store. 184x184 JPG, no alpha.
- **Mac Icon: (ICNS)** and Linux icons (PNG sizes in a zip) are optional.

A demo uses the main game's icons.
