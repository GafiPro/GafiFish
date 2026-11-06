namespace GafiFish.App.Automation;

public sealed record AquariumAutomationOptions(
    bool FeedEnabled,
    bool RestockEnabled,
    bool ClaimEnabled)
{
    public bool HasEnabledActions => FeedEnabled || RestockEnabled || ClaimEnabled;
}
