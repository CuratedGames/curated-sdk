using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal sealed class RecordingTransport : ITransport
{
    private readonly Func<string, string, object?, string> _responder;

    public RecordingTransport(Func<string, string, object?, string>? responder = null)
    {
        _responder = responder ?? DefaultResponder;
    }

    public List<RecordedCall> Calls { get; } = new();

    public Task<string> SendAsync(
        string method,
        string relativePath,
        object? jsonBody,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls.Add(new RecordedCall(method, relativePath, jsonBody));
        return Task.FromResult(_responder(method, relativePath, jsonBody));
    }

    private static string DefaultResponder(string method, string path, object? body)
    {
        if (path.Contains("/achievements/", StringComparison.Ordinal)
            && path.EndsWith("/unlock", StringComparison.Ordinal)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            var apiName = Uri.UnescapeDataString(path.Split('/')[^2]);
            return Json.Serialize(
                new UnlockResult
                {
                    AppId = 0,
                    ApiName = apiName,
                    NewlyUnlocked = true,
                    Source = "native"
                });
        }

        if (path.EndsWith("/cloud-save/sync", StringComparison.Ordinal)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            var direction = "download";
            var urls = new List<CloudSaveSignedUrl>();
            if (body is CloudSaveSyncRequest req)
            {
                direction = req.Direction;
                if (string.Equals(direction, "upload", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var f in req.Files)
                    {
                        urls.Add(new CloudSaveSignedUrl
                        {
                            RelativePath = f.RelativePath,
                            Action = "put",
                            ContentHash = f.ContentHash,
                            SizeBytes = f.SizeBytes,
                            Url =
                                $"https://cdn.test/cloud-save/content?key={Uri.EscapeDataString(f.RelativePath)}&dir=upload&sig=test"
                        });
                    }
                }
            }

            return Json.Serialize(
                new CloudSaveSyncResultWire
                {
                    Direction = direction,
                    ConflictPolicy = "last_write_wins",
                    Urls = urls
                });
        }

        if (path.EndsWith("/cloud-save/manifest", StringComparison.Ordinal)
            && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return Json.Serialize(
                new { app_id = 0, enabled = true, conflict_policy = "last_write_wins", files = Array.Empty<object>() });
        }

        if (path.EndsWith("/achievements/progress", StringComparison.Ordinal))
        {
            return Json.Serialize(
                new AchievementProgressResult
                {
                    Unlocked = 0,
                    Total = 0,
                    Achievements = Array.Empty<AchievementProgressItem>()
                });
        }

        if (path.EndsWith("/achievements/me", StringComparison.Ordinal)
            || path.EndsWith("/achievements", StringComparison.Ordinal))
        {
            return Json.Serialize(
                new AchievementListResult { Achievements = Array.Empty<AchievementProgressItem>() });
        }

        if (path.Equals("/friends", StringComparison.Ordinal)
            && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return "[]";
        }

        if (path.Equals("/friends/requests", StringComparison.Ordinal))
        {
            return "[]";
        }

        if (path.Equals("/friends/presence", StringComparison.Ordinal))
        {
            return "[]";
        }

        if (path.Equals("/friends/request", StringComparison.Ordinal)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            return Json.Serialize(new { status = "pending" });
        }

        if (path.Contains("/friends/", StringComparison.Ordinal)
            && path.EndsWith("/accept", StringComparison.Ordinal))
        {
            return Json.Serialize(new { status = "accepted" });
        }

        if (path.Equals("/presence", StringComparison.Ordinal))
        {
            return Json.Serialize(
                new PresenceRow
                {
                    Status = "online",
                    RichText = "",
                    UpdatedAt = DateTimeOffset.UtcNow
                });
        }

        if (path.Equals("/presence/heartbeat", StringComparison.Ordinal))
        {
            return Json.Serialize(
                new PresenceRow
                {
                    Status = "online",
                    RichText = "",
                    UpdatedAt = DateTimeOffset.UtcNow
                });
        }

        if (path.Equals("/auth/openid/id-token", StringComparison.Ordinal)
            && method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            return Json.Serialize(
                new OpenIdIdTokenWire
                {
                    IdToken = "header.payload.signature",
                    ExpiresIn = 600
                });
        }

        return Json.Serialize(new { ok = true });
    }
}

internal sealed record RecordedCall(string Method, string RelativePath, object? Body);
