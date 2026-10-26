using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class SessionService
{
    public static async Task<CuratedSession> StartAsync(
        CuratedClient.ClientState state,
        LaunchContext context,
        CancellationToken cancellationToken)
    {
        var user = await SendAsync<SessionUserWire>(state, "/auth/me", cancellationToken).ConfigureAwait(false);
        if (user.UserId == Guid.Empty || string.IsNullOrWhiteSpace(user.CuratedId))
        {
            throw new CuratedLaunchException(CuratedLaunchError.HubError, "Curated returned an incomplete player profile.");
        }

        var entitlement = await CheckEntitlementAsync(state, cancellationToken).ConfigureAwait(false);
        if (!entitlement.IsEntitled)
        {
            throw new CuratedLaunchException(CuratedLaunchError.NotEntitled, "This Curated account does not own the game.");
        }

        return new CuratedSession(state.AppId, user.UserId, user.CuratedId.Trim(), user.Username, context.Language, entitlement);
    }

    public static async Task<Entitlement> CheckEntitlementAsync(
        CuratedClient.ClientState state,
        CancellationToken cancellationToken)
    {
        var items = await SendAsync<List<EntitlementWire>>(state, "/library", cancellationToken).ConfigureAwait(false);
        foreach (var item in items)
        {
            if (item.AppId == state.AppId)
            {
                return new Entitlement(state.AppId, true, item.Source, item.AcquiredAt);
            }
        }

        return new Entitlement(state.AppId, false, null, null);
    }

    private static async Task<T> SendAsync<T>(
        CuratedClient.ClientState state,
        string path,
        CancellationToken cancellationToken)
        where T : class
    {
        string body;
        try
        {
            body = await state.Transport.SendAsync("GET", path, null, cancellationToken).ConfigureAwait(false);
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
            throw new CuratedLaunchException(CuratedLaunchError.HubError, "Curated could not verify the session.", e);
        }
        catch (HttpRequestException e)
        {
            throw new CuratedLaunchException(CuratedLaunchError.NetworkError, "Could not reach Curated.", e);
        }

        try
        {
            return Json.Deserialize<T>(body)
                ?? throw new FormatException("empty body");
        }
        catch (FormatException e)
        {
            throw new CuratedLaunchException(CuratedLaunchError.HubError, "Curated returned an unexpected response.", e);
        }
    }
}
