namespace GafiFish.Core.Control;

/// <summary>
/// Selects the visual minigame mechanic for a named rod profile.
/// StandardReel uses the shared fish/bar controller. ChargeMeterBonus adds
/// a calibrated charge-release action on top of that controller.
/// </summary>
public enum RodMinigameMode
{
    StandardReel,
    ChargeMeterBonus,
    DualFish
}
