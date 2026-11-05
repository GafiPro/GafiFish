namespace GafiFish.Core.Control;

public sealed record ReelControlSettings
{
    public double ProportionalGain { get; init; } = 0.75;
    public double DerivativeGain { get; init; } = 0.10;
    public double Deadband { get; init; } = 10;
    public double LookAheadSeconds { get; init; } = 0.09;
    public double MinimumConfidence { get; init; } = 0.65;
    public bool PositiveErrorMeansHold { get; init; } = true;

    public void Validate()
    {
        if (!double.IsFinite(ProportionalGain) || ProportionalGain is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(ProportionalGain));
        if (!double.IsFinite(DerivativeGain) || DerivativeGain is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(DerivativeGain));
        if (!double.IsFinite(Deadband) || Deadband is < 0 or > 500)
            throw new ArgumentOutOfRangeException(nameof(Deadband));
        if (!double.IsFinite(LookAheadSeconds) || LookAheadSeconds is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(LookAheadSeconds));
        if (!double.IsFinite(MinimumConfidence) || MinimumConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumConfidence));
    }
}
