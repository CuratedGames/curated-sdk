# OpenID Connect

After the launch check, a game can mint a short-lived RS256 ID token for its own OpenID Connect
provider, for example Unity Authentication.

- Provider name in Unity: `oidc-curated`
- Audience: `curated-app-<app id>`

| Binding | Call |
| --- | --- |
| .NET | `CuratedClient.GetOpenIdIdTokenAsync()` |
| Unity | `CuratedUnity.GetOpenIdIdTokenAsync()` |
| Godot | `Curated.get_openid_id_token()` |
| Unreal | `GetOpenIdIdToken(OnDone)` |
| RPG Maker | `Curated.getOpenIdIdToken()` |

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#openid{csharp}

Rules:

- Pass the ID token only to your OpenID Connect consumer. Never log it, never put it in the
  environment.
- `CURATED_SESSION_TOKEN` still goes only to `CURATED_API_BASE`. Never send it to Unity or any
  other service.
