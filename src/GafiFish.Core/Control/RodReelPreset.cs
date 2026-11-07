namespace GafiFish.Core.Control;

public enum RodReelPreset
{
    Balanced,
    Precision,
    HeavyFish,
    Stable
}

/// <summary>
/// Supplies conservative controller profiles that can be saved in each named
/// rod calibration profile. These presets tune the existing marker-based Reel
/// minigame; they do not claim support for unrelated rod-specific UI mechanics.
/// </summary>
public static class RodReelPresetFactory
{
    public static ReelControlSettings Create(RodReelPreset preset)
    {
        var settings = preset switch
        {
            RodReelPreset.Balanced => new ReelControlSettings
            {
                ProportionalGain = 0.75,
                DerivativeGain = 0.10,
                Deadband = 10,
                LookAheadSeconds = 0.09
            },
            RodReelPreset.Precision => new ReelControlSettings
            {
                ProportionalGain = 1.00,
                DerivativeGain = 0.08,
                Deadband = 5,
                LookAheadSeconds = 0.05
            },
            RodReelPreset.HeavyFish => new ReelControlSettings
            {
                ProportionalGain = 0.70,
                DerivativeGain = 0.16,
                Deadband = 15,
                LookAheadSeconds = 0.14
            },
            RodReelPreset.Stable => new ReelControlSettings
            {
                ProportionalGain = 0.55,
                DerivativeGain = 0.18,
                Deadband = 18,
                LookAheadSeconds = 0.12
            },
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };

        settings.Validate();
        return settings;
    }
}
