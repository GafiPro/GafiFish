namespace GafiFish.Core.Aquarium;

/// <summary>
/// Emits one readiness event per completed action timestamp. It does not
/// repeat alerts every timer tick and treats each food action independently.
/// </summary>
public sealed class AquariumReadyAlertTracker
{
    private readonly Dictionary<string, DateTimeOffset> _lastAlertedAt =
        new(StringComparer.Ordinal);

    public IReadOnlyList<string> Evaluate(AquariumScheduleState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();

        var actions = new List<(string Key, string Label, DateTimeOffset? LastCompletedAt)>
        {
            ("GuppiesBest.Feed", "Guppies' Best · feeding", state.GuppiesBest.LastFedAt),
            ("GuppiesBest.Restock", "Guppies' Best · restock", state.GuppiesBest.LastRestockedAt),
            ("FishChow.Feed", "Fish Chow · feeding", state.FishChow.LastFedAt),
            ("FishChow.Restock", "Fish Chow · restock", state.FishChow.LastRestockedAt),
            ("SplashSnacks.Feed", "Splash Snacks · feeding", state.SplashSnacks.LastFedAt),
            ("SplashSnacks.Restock", "Splash Snacks · restock", state.SplashSnacks.LastRestockedAt)
        };

        if (state.ClaimReminderEnabled)
            actions.Add(("RewardClaim", "Aquarium reward claim", state.LastClaimedAt));

        var ready = new List<string>();
        foreach (var action in actions)
        {
            if (action.LastCompletedAt is not DateTimeOffset completedAt)
                continue;

            var evaluation = AquariumCooldownPolicy.Evaluate(completedAt, now);
            if (evaluation.Status != AquariumCooldownStatus.Ready)
                continue;

            if (_lastAlertedAt.TryGetValue(action.Key, out var lastAlerted)
                && lastAlerted == completedAt)
                continue;

            _lastAlertedAt[action.Key] = completedAt;
            ready.Add(action.Label);
        }

        return ready;
    }

    public void Reset() => _lastAlertedAt.Clear();
}
