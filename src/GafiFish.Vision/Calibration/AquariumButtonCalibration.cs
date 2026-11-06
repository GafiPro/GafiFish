namespace GafiFish.Vision.Calibration;

/// <summary>
/// User-sampled ready-state button appearance. Empty by default so the
/// application never guesses an Aquarium button location or colour.
/// </summary>
public sealed record AquariumButtonCalibration
{
    public NormalizedRegion Region { get; init; } = NormalizedRegion.FullFrame;
    public bool RegionCalibrated { get; init; }
    public HsvColorRange? ButtonColor { get; init; }

    public bool IsCalibrated => RegionCalibrated && ButtonColor.HasValue;

    public void Validate()
    {
        Region.Validate();
        ButtonColor?.Validate();
    }
}
