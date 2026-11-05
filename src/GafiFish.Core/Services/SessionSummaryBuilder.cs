using GafiFish.Core.Models;

namespace GafiFish.Core.Services;

/// <summary>
/// Builds the end-of-session summary. The full collection remains available to
/// the caller for persistence/export; only TopCatches is truncated to 25.
/// </summary>
public static class SessionSummaryBuilder
{
    public const int DefaultTopLimit = 25;

    public static SessionSummary Build(
        IEnumerable<FishCatch> catches,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        int topLimit = DefaultTopLimit)
    {
        ArgumentNullException.ThrowIfNull(catches);

        if (topLimit is < 1 or > 25)
        {
            throw new ArgumentOutOfRangeException(
                nameof(topLimit),
                "The session summary may show between 1 and 25 fish.");
        }

        if (endedAt < startedAt)
        {
            throw new ArgumentException("The session end cannot be earlier than its start.");
        }

        var allCatches = catches.ToArray();
        var duration = endedAt - startedAt;

        var ranked = allCatches
            .OrderByDescending(c => c.Rarity ?? FishRarity.Unknown)
            .ThenByDescending(c => !string.IsNullOrWhiteSpace(c.Mutation))
            .ThenByDescending(c => c.WeightKg ?? 0d)
            .ThenByDescending(c => c.EstimatedValue ?? 0m)
            .ThenByDescending(c => c.CaughtAt)
            .Take(topLimit)
            .ToArray();

        var catchesPerHour = duration.TotalHours > 0
            ? allCatches.Length / duration.TotalHours
            : 0d;

        return new SessionSummary(
            TotalCatches: allCatches.Length,
            IdentifiedCatches: allCatches.Count(c => c.IsIdentified),
            Duration: duration,
            CatchesPerHour: catchesPerHour,
            TopCatches: ranked,
            WasTruncated: allCatches.Length > topLimit);
    }
}
