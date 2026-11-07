namespace GafiFish.Vision.Calibration;

/// <summary>
/// User-sampled button region and enabled-state colour for a one-shot Fisch
/// action. No location or colour is guessed by default.
/// </summary>
public sealed record FischUiActionCalibration
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
