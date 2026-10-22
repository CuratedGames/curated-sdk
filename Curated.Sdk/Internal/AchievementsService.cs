using System;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal static class AchievementsService
{
    public static async Task<UnlockResult> UnlockAsync(
        CuratedClient.ClientState state,
        string apiName,
        CancellationToken ct)
    {
        var trimmed = apiName.Trim();
        var json = await state.Transport
            .SendAsync(
                "POST",
                $"/games/app/{state.AppId}/achievements/{Uri.EscapeDataString(trimmed)}/unlock",
                new { },
                ct)
            .ConfigureAwait(false);
        var result = Deserialize<UnlockResult>(json);
        if (result.AppId == 0)
        {
            result.AppId = state.AppId;
        }

        if (string.IsNullOrEmpty(result.ApiName))
        {
            result.ApiName = trimmed;
        }

        return result;
    }

    public static async Task<AchievementProgressResult> ProgressAsync(
        CuratedClient.ClientState state,
        CancellationToken ct)
    {
        var json = await state.Transport
            .SendAsync("GET", $"/games/app/{state.AppId}/achievements/progress", null, ct)
            .ConfigureAwait(false);
        return Deserialize<AchievementProgressResult>(json);
    }

    private static T Deserialize<T>(string json)
    {
        var value = Json.Deserialize<T>(json);
        if (value is null)
        {
            throw new InvalidOperationException($"failed to deserialize {typeof(T).Name}");
        }

        return value;
    }
}
