using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class FriendsService
{
    public static async Task<IReadOnlyList<Friend>> GetFriendsAsync(
        CuratedClient.ClientState state,
        CancellationToken ct)
    {
        var friendsJson = await state.Transport
            .SendAsync("GET", "/friends", null, ct)
            .ConfigureAwait(false);
        var rows = DeserializeList<FriendRow>(friendsJson);

        Dictionary<Guid, PresenceRow> presenceById;
        try
        {
            var presenceJson = await state.Transport
                .SendAsync("GET", "/friends/presence", null, ct)
                .ConfigureAwait(false);
            presenceById = DeserializeList<PresenceRow>(presenceJson)
                .ToDictionary(p => p.UserId, p => p);
        }
        catch (CuratedException)
        {
            presenceById = new Dictionary<Guid, PresenceRow>();
        }

        var list = new List<Friend>(rows.Count);
        foreach (var row in rows)
        {
            presenceById.TryGetValue(row.UserId, out var presence);
            list.Add(new Friend(
                row.UserId,
                row.Username,
                presence?.Status ?? "offline",
                presence?.RichText ?? "",
                presence?.AppId));
        }

        return list;
    }

    public static async Task RequestAsync(
        CuratedClient.ClientState state,
        string username,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("username is required", nameof(username));
        }

        await state.Transport
            .SendAsync("POST", "/friends/request", new { username = username.Trim() }, ct)
            .ConfigureAwait(false);
    }

    public static async Task AcceptAsync(
        CuratedClient.ClientState state,
        Guid userId,
        CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("user id is required", nameof(userId));
        }

        await state.Transport
            .SendAsync("POST", $"/friends/{userId:D}/accept", new { }, ct)
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<Friend>> GetRequestsAsync(
        CuratedClient.ClientState state,
        CancellationToken ct)
    {
        var json = await state.Transport
            .SendAsync("GET", "/friends/requests", null, ct)
            .ConfigureAwait(false);
        var rows = DeserializeList<FriendRow>(json);
        return rows
            .Select(r => new Friend(r.UserId, r.Username, "offline", "", null))
            .ToList();
    }

    public static async Task SetPresenceAsync(
        CuratedClient.ClientState state,
        string status,
        string? richText,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("status is required", nameof(status));
        }

        var trimmed = status.Trim();
        long? appId = string.Equals(trimmed, "in_game", StringComparison.OrdinalIgnoreCase)
            ? state.AppId
            : null;

        await state.Transport
            .SendAsync(
                "PUT",
                "/presence",
                new SetPresenceRequest
                {
                    Status = trimmed,
                    RichText = richText ?? "",
                    AppId = appId
                },
                ct)
            .ConfigureAwait(false);
    }

    public static async Task HeartbeatAsync(CuratedClient.ClientState state, CancellationToken ct)
    {
        await state.Transport
            .SendAsync("POST", "/presence/heartbeat", new { }, ct)
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<Friend>> GetFriendsPresenceAsync(
        CuratedClient.ClientState state,
        CancellationToken ct)
    {
        var json = await state.Transport
            .SendAsync("GET", "/friends/presence", null, ct)
            .ConfigureAwait(false);
        var rows = DeserializeList<PresenceRow>(json);
        return rows
            .Select(p => new Friend(
                p.UserId,
                p.Username ?? "",
                p.Status,
                p.RichText,
                p.AppId))
            .ToList();
    }

    private static IReadOnlyList<T> DeserializeList<T>(string json)
    {
        var value = Json.Deserialize<List<T>>(json);
        return value ?? new List<T>();
    }
}
