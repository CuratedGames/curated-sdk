# Depots

**Game Build > Depots** splits your game files by operating system, architecture, and language.
Upload builds into a depot with Content Builder's `--depot-id`.

## Depot IDs

Curated assigns each depot an ID from `<app_id>_00` to `<app_id>_99`. IDs are never reused.
`<app_id>_00` is the base depot, and the default target of `curated-content-builder upload`.
Depots can be edited but not deleted, because builds and player installs depend on them.

## Add a depot

Under **Add depot**, fill in:

- **Name**, for example "Italian voice-over".
- **Operating system**: Any OS, Windows, Mac, or Linux.
- **Architecture**: Any arch, 32-bit, 64-bit, or ARM64.
- **Language**: a specific language, or all languages.

Click **Create depot**. The new ID is shown before you save. To change one later, click **Edit**,
then **Save depot**.

Then upload to it:

```bat
curated-content-builder.exe upload --app-id 1000 --depot-id 1000_01 --content C:\game\it-voice --version 1.2.3
```

## Depot localization

Pick one approach based on the size of your localized files.

**Separate language depots** (large data, such as full voice-over or localized video). Put shared
files in the base depot and each language in its own depot. Players download only the language
their launcher uses.

**Single "All Languages" depot** (small data, such as text and fonts). Ship every language in one
depot. The game reads the player's language at runtime with
[`GetCurrentGameLanguage`](/guide/game-language).

## Packages

Packages decide which depots a purchase grants. See
[Packages and product keys](./packages).
