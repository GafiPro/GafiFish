namespace GafiFish.App.Settings;

public sealed record CatchAlertSettings
{
    public string WatchedSpecies { get; init; } = string.Empty;
    public bool AlertOnLegendaryOrHigher { get; init; } = true;
    public bool AlertOnMutation { get; init; }

    public void Validate()
    {
        if (WatchedSpecies is null || WatchedSpecies.Length > 2000)
            throw new ArgumentException("The watched species list must be 2,000 characters or fewer.");
    }
}
