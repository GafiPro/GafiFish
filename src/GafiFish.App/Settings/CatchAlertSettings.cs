namespace GafiFish.App.Settings;

public sealed record CatchAlertSettings
{
    public string WatchedSpecies { get; init; } = string.Empty;
    public bool AlertOnLegendaryOrHigher { get; init; } = true;
    public bool AlertOnMutation { get; init; }
    public double? MinimumWeightKg { get; init; }
    public decimal? MinimumEstimatedValue { get; init; }

    public void Validate()
    {
        if (WatchedSpecies is null || WatchedSpecies.Length > 2000)
            throw new ArgumentException("The watched species list must be 2,000 characters or fewer.");
        if (MinimumWeightKg.HasValue
            && (!double.IsFinite(MinimumWeightKg.Value) || MinimumWeightKg.Value <= 0))
            throw new ArgumentException("Minimum weight must be a finite positive number.");
        if (MinimumEstimatedValue.HasValue && MinimumEstimatedValue.Value <= 0)
            throw new ArgumentException("Minimum estimated value must be a positive number.");
    }
}
