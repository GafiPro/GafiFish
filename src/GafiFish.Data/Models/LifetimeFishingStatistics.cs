namespace GafiFish.Data.Models;

public sealed record LifetimeFishingStatistics(
    int TotalSessions,
    int TotalCatches,
    int IdentifiedCatches,
    int MutatedCatches,
    int LegendaryOrHigherCatches,
    decimal TotalEstimatedValue,
    double? AverageWeightKg,
    double? HeaviestWeightKg,
    double CompletedFishingHours,
    double CatchesPerHour,
    IReadOnlyList<SpeciesCatchStatistic> TopSpecies);
