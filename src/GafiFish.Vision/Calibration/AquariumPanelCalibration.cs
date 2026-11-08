namespace GafiFish.Vision.Calibration;

/// <summary>
/// A user-calibrated colour patch that is visible only while the Aquarium panel is open.
/// The region should be small, uniform and away from interactive buttons.
/// </summary>
public sealed record AquariumPanelCalibration
{
    public NormalizedRegion Region { get; init; } = NormalizedRegion.FullFrame;
    public bool RegionCalibrated { get; init; }
    public HsvColorRange? PanelColor { get; init; }
    public double MinimumMatchRatio { get; init; } = 0.60;

    public bool IsCalibrated => RegionCalibrated && PanelColor.HasValue;

    public void Validate()
    {
        Region.Validate();
        PanelColor?.Validate();
        if (!double.IsFinite(MinimumMatchRatio) || MinimumMatchRatio is < 0.10 or > 1)
            throw new ArgumentOutOfRangeException(nameof(MinimumMatchRatio));
    }
}
