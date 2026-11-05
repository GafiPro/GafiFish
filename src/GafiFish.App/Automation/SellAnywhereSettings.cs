namespace GafiFish.App.Automation;

public sealed record SellAnywhereSettings
{
    /// <summary>Fisch inventory toggle key (US backtick key by default).</summary>
    public ushort InventoryToggleVirtualKey { get; init; } = 0xC0;

    public double MinimumButtonConfidence { get; init; } = 0.82;
    public double MinimumInventoryChangeRatio { get; init; } = 0.04;
    public double MaximumStableCenterDriftPixels { get; init; } = 14;
    public TimeSpan PhaseTimeout { get; init; } = TimeSpan.FromSeconds(8);
    public TimeSpan VerificationDelay { get; init; } = TimeSpan.FromMilliseconds(650);
    public int RequiredStableButtonFrames { get; init; } = 2;
    public int RequiredVerificationFrames { get; init; } = 2;

    public void Validate()
    {
        if (InventoryToggleVirtualKey == 0)
            throw new ArgumentOutOfRangeException(nameof(InventoryToggleVirtualKey));
        if (!double.IsFinite(MinimumButtonConfidence) || MinimumButtonConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumButtonConfidence));
        if (!double.IsFinite(MinimumInventoryChangeRatio) || MinimumInventoryChangeRatio is < 0.001 or > 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumInventoryChangeRatio));
        if (!double.IsFinite(MaximumStableCenterDriftPixels) || MaximumStableCenterDriftPixels is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(MaximumStableCenterDriftPixels));
        if (PhaseTimeout <= TimeSpan.Zero || VerificationDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(PhaseTimeout));
        if (RequiredStableButtonFrames is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(RequiredStableButtonFrames));
        if (RequiredVerificationFrames is < 2 or > 5)
            throw new ArgumentOutOfRangeException(nameof(RequiredVerificationFrames));
    }
}
