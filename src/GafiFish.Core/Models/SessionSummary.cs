namespace GafiFish.Core.Models;

public sealed record SessionSummary(
    int TotalCatches,
    int IdentifiedCatches,
    TimeSpan Duration,
    double CatchesPerHour,
    IReadOnlyList<FishCatch> TopCatches,
    bool WasTruncated);
