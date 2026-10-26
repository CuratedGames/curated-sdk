using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class OpenIdService
{
    public static async Task<string> MintIdTokenAsync(
        CuratedClient.ClientState state,
        CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await state.Transport
                .SendAsync(
                    "POST",
                    "/auth/openid/id-token",
                    new OpenIdIdTokenRequest { AppId = state.AppId },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CuratedException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.SessionInvalid,
                "The Curated session expired. Relaunch the game from Curated.",
                e);
        }
        catch (CuratedException e)
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.HubError,
                "Curated could not mint an OpenID ID token.",
                e);
        }
        catch (HttpRequestException e)
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.NetworkError,
                "Could not reach Curated.",
                e);
        }

        try
        {
            var wire = Json.Deserialize<OpenIdIdTokenWire>(body)
                ?? throw new FormatException("empty body");
            if (string.IsNullOrWhiteSpace(wire.IdToken))
            {
                throw new CuratedLaunchException(
                    CuratedLaunchError.HubError,
                    "Curated returned an empty OpenID ID token.");
            }

            return wire.IdToken.Trim();
        }
        catch (FormatException e)
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.HubError,
                "Curated returned an unexpected OpenID response.",
                e);
        }
    }
}
