namespace GafiFish.Core.Casting;

public sealed record PerfectCastSettings
{
    public TimeSpan EstimatedInputLatency { get; init; } = TimeSpan.FromMilliseconds(50);
    public TimeSpan MaximumFrameAge { get; init; } = TimeSpan.FromMilliseconds(200);
    public double MinimumConfidence { get; init; } = 0.85;

    public void Validate()
    {
        if (EstimatedInputLatency < TimeSpan.Zero || EstimatedInputLatency > TimeSpan.FromMilliseconds(500))
        {
            throw new ArgumentOutOfRangeException(nameof(EstimatedInputLatency));
        }

        if (MaximumFrameAge <= TimeSpan.Zero || MaximumFrameAge > TimeSpan.FromSeconds(2))
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumFrameAge));
        }

        if (MinimumConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumConfidence));
        }
    }
}
