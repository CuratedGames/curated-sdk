using System;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>One achievement defined for the current app.</summary>
public sealed class Achievement
{
    private bool _state;
    private string _displayName;
    private string _description;
    private DateTimeOffset? _unlockedAt;

    /// <summary>Create a handle for the achievement with this name (matches admin Achievements tool).</summary>
    public Achievement(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("achievement name is required", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 64)
        {
            throw new ArgumentException("achievement name max length is 64", nameof(name));
        }

        Name = trimmed;
        _displayName = trimmed;
        _description = "";
    }

    internal Achievement(
        string name,
        string displayName,
        string description,
        bool unlocked,
        DateTimeOffset? unlockedAt)
    {
        Name = name;
        _displayName = displayName;
        _description = description;
        _state = unlocked;
        _unlockedAt = unlockedAt;
    }

    /// <summary>Stable name used when unlocking (same string you configure in admin).</summary>
    public string Name { get; }

    /// <summary>Player-facing title when progress has been loaded; otherwise equals <see cref="Name"/>.</summary>
    public string DisplayName => _displayName;

    /// <summary>Player-facing description when progress has been loaded.</summary>
    public string Description => _description;

    /// <summary>True when the local player has unlocked this achievement.</summary>
    public bool State => _state;

    /// <summary>When the achievement was unlocked, if known.</summary>
    public DateTimeOffset? UnlockedAt => _unlockedAt;

    /// <summary>
    /// Unlock this achievement for the local player. Safe to call more than once.
    /// Returns true when this call newly unlocked it.
    /// </summary>
    public async Task<bool> TriggerAsync(CancellationToken cancellationToken = default)
    {
        var result = await AchievementsService
            .UnlockAsync(CuratedClient.GameState, Name, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrEmpty(result.DisplayName))
        {
            _displayName = result.DisplayName;
        }

        if (!string.IsNullOrEmpty(result.Description))
        {
            _description = result.Description;
        }

        if (result.NewlyUnlocked || result.Unlocked)
        {
            _state = true;
            _unlockedAt ??= DateTimeOffset.UtcNow;
        }

        if (result.NewlyUnlocked)
        {
            OverlayNotifier.NotifyAchievementUnlocked(
                result.AppId != 0 ? result.AppId : CuratedClient.GameState.AppId,
                string.IsNullOrEmpty(result.ApiName) ? Name : result.ApiName,
                string.IsNullOrEmpty(result.DisplayName) ? _displayName : result.DisplayName,
                string.IsNullOrEmpty(result.Description) ? _description : result.Description,
                result.IconAchievedUri,
                result.GlobalUnlockPercent);
        }

        return result.NewlyUnlocked;
    }

    internal void ApplyProgress(AchievementProgressItem item)
    {
        if (!string.IsNullOrEmpty(item.DisplayName))
        {
            _displayName = item.DisplayName;
        }

        if (!string.IsNullOrEmpty(item.Description))
        {
            _description = item.Description;
        }

        _state = item.Unlocked;
        _unlockedAt = item.UnlockedAt;
    }
}
