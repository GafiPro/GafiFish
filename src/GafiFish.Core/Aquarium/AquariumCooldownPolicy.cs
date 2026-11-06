namespace GafiFish.Core.Aquarium;

public enum AquariumCooldownStatus
{
    NotTracked,
    CoolingDown,
    Ready
}

public sealed record AquariumCooldownEvaluation(
    AquariumCooldownStatus Status,
    DateTimeOffset? NextEligibleAt,
    TimeSpan? Remaining);

/// <summary>
/// Tracks the hourly feed, restock and reward-claim windows used by Fisch's
/// Personal Aquarium. This policy only reports readiness; it never sends input.
/// </summary>
public static class AquariumCooldownPolicy
{
    public static TimeSpan Cooldown { get; } = TimeSpan.FromHours(1);

    public static AquariumCooldownEvaluation Evaluate(
        DateTimeOffset? lastCompletedAt,
        DateTimeOffset now)
    {
        if (!lastCompletedAt.HasValue)
        {
            return new AquariumCooldownEvaluation(
                AquariumCooldownStatus.NotTracked,
                null,
                null);
        }

        var nextEligibleAt = lastCompletedAt.Value + Cooldown;
        if (now >= nextEligibleAt)
        {
            return new AquariumCooldownEvaluation(
                AquariumCooldownStatus.Ready,
                nextEligibleAt,
                TimeSpan.Zero);
        }

        return new AquariumCooldownEvaluation(
            AquariumCooldownStatus.CoolingDown,
            nextEligibleAt,
            nextEligibleAt - now);
    }
}
