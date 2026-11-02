# Testing outside the launcher

You do not need the launcher for every test run.

## Set the variables yourself

Set the launch variables in your IDE or shell. Use a session token from your own account: the
`token` that `curated-content-builder login` saves in `%USERPROFILE%\.curated\credentials.json`
(`~/.curated/credentials.json` on Linux and macOS).

```bat
set CURATED_LAUNCHER=1
set CURATED_CONTRACT_VERSION=1
set CURATED_APP_ID=1002
set CURATED_SESSION_TOKEN=<token from credentials.json>
```

Your account needs an entitlement for the app. This token is not limited to one game, so keep it
out of builds you share.

## Engine shortcuts

- **Unity**: in the Editor, `CuratedUnity.InitFromLauncherAsync` falls back to the account signed
  in to a running Curated launcher when the variables are missing. The bootstrap component can
  skip the check in the Editor.
- **RPG Maker**: test play from the editor skips the check.
- **Godot, Unreal, .NET**: set the variables above.

## Run the binding tests

```bash
dotnet test Curated.Sdk.sln --configuration Release
node --test engines/rpgmaker/CuratedSDK.test.mjs
```

Godot and Unreal tests run inside the engine. See [Godot 4](./godot#tests) and
[Unreal Engine 5](./unreal#tests).
