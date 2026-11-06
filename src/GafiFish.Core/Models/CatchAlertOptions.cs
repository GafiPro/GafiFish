namespace GafiFish.Core.Models;

/// <summary>
/// Local-only notification preferences. Species are matched as case-insensitive
/// fragments of a recognised species name.
/// </summary>
public sealed record CatchAlertOptions(
    IReadOnlyList<string> WatchedSpecies,
    bool AlertOnLegendaryOrHigher,
    bool AlertOnMutation);
