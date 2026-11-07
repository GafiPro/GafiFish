namespace GafiFish.Core.Automation;

/// <summary>
/// Day/night state detected from a user-calibrated, visible game indicator.
/// Unknown is deliberately not interpreted as either phase.
/// </summary>
public enum DayNightPhase
{
    Unknown,
    Day,
    Night
}
