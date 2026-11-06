namespace GafiFish.App.Automation;

public sealed record ShakeInputSettings
{
    public double MinimumConfidence { get; init; } = 0.68;
    public double MaximumCandidateDriftPixels { get; init; } = 14;
    public double MinimumPromptDisplacementPixels { get; init; } = 34;
    public TimeSpan MinimumClickInterval { get; init; } = TimeSpan.FromMilliseconds(400);
    public TimeSpan MaximumFrameAge { get; init; } = TimeSpan.FromMilliseconds(250);
    public int RequiredStableFrames { get; init; } = 2;
    public int RequiredMissingFrames { get; init; } = 4;

    public void Validate()
    {
        if (!double.IsFinite(MinimumConfidence) || MinimumConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumConfidence));
        if (!double.IsFinite(MaximumCandidateDriftPixels) || MaximumCandidateDriftPixels is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(MaximumCandidateDriftPixels));
        if (!double.IsFinite(MinimumPromptDisplacementPixels) || MinimumPromptDisplacementPixels is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(MinimumPromptDisplacementPixels));
        if (MinimumClickInterval < TimeSpan.Zero || MaximumFrameAge <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(MinimumClickInterval));
        if (RequiredStableFrames is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(RequiredStableFrames));
        if (RequiredMissingFrames is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(RequiredMissingFrames));
    }
}
