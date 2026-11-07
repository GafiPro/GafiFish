namespace GafiFish.Vision.Calibration;

/// <summary>
/// User-calibrated appearance of one Angler dialog button. Empty by default:
/// the app never guesses the button's screen position or colour.
/// </summary>
public sealed record AnglerQuestButtonCalibration
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
