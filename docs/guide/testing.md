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

- **Unity**: Editor play can sign in through a running launcher, without the variables above.
  See [Unity](./unity#sign-in-and-check-ownership).
- **RPG Maker**: test play from the editor skips the check.
- **Godot, Unreal, .NET**: set the variables above.
