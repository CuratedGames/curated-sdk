# Builds

**Game Build > Builds** lists every build you uploaded with [Content Builder](/guide/content-builder)
and lets you set one live.

## Release channels

There are two channels: **Default** (what every owner downloads) and **Beta** (players who opted
into beta). Each depot keeps its own live build per channel. The status rows at the top show what
is live, for example `1000_00 live 1.2.3`.

## Set a build live

1. Upload with Content Builder. The build appears here with its version, depot, and build id.
2. In **Set live on channel**, pick **Default** or **Beta**.
3. Click the set live button on the build (it names the channel you picked).

A build marked "not ready" is still processing and cannot be set live yet.

## Changelog and download

**Edit changelog** adds release notes to a build (markdown). **Download** fetches the uploaded
files.

## Retention

Builds that are not live on any channel are removed after 14 days of inactivity. The row shows
the deletion date.
