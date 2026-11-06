namespace GafiFish.Core.Models;

/// <summary>
/// Local-only notification preferences. Species are matched as case-insensitive
/// fragments of a recognised species name. Numeric thresholds are optional and
/// only match when OCR has supplied the corresponding field.
/// </summary>
public sealed record CatchAlertOptions(
    IReadOnlyList<string> WatchedSpecies,
    bool AlertOnLegendaryOrHigher,
    bool AlertOnMutation,
    double? MinimumWeightKg = null,
    decimal? MinimumEstimatedValue = null);
