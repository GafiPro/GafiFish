using GafiFish.Core.Aquarium;
namespace GafiFish.Vision.Calibration;

/// <summary>
/// Per-resolution fishing UI calibration. Color ranges are deliberately empty
/// until the user samples the actual game UI; no guessed colors are enabled.
/// </summary>
public sealed record VisionProfile
{
    public int ReferenceWidth { get; init; }
    public int ReferenceHeight { get; init; }
    public NormalizedRegion FishingRegion { get; init; } = NormalizedRegion.FullFrame;
    public NormalizedRegion CastRegion { get; init; } = NormalizedRegion.FullFrame;
    public NormalizedRegion ShakeRegion { get; init; } = NormalizedRegion.FullFrame;
    public NormalizedRegion SellButtonRegion { get; init; } = NormalizedRegion.FullFrame;
    public NormalizedRegion InventoryItemsRegion { get; init; } = NormalizedRegion.FullFrame;
    public NormalizedRegion CatchNotificationRegion { get; init; } = NormalizedRegion.FullFrame;
    public bool CatchNotificationRegionCalibrated { get; init; }
    public NormalizedRegion ReconnectButtonRegion { get; init; } = NormalizedRegion.FullFrame;
    public bool ReconnectButtonRegionCalibrated { get; init; }
    public HsvColorRange? ReconnectButtonColor { get; init; }
    public bool FishingRegionCalibrated { get; init; }
    public bool CastRegionCalibrated { get; init; }
    public bool ShakeRegionCalibrated { get; init; }
    public bool SellButtonRegionCalibrated { get; init; }
    public bool InventoryItemsRegionCalibrated { get; init; }
    public HsvColorRange? FishMarkerColor { get; init; }
    public HsvColorRange? ControlMarkerColor { get; init; }
    public HsvColorRange? CastIndicatorColor { get; init; }
    public HsvColorRange? CastTargetColor { get; init; }
    public HsvColorRange? ShakePromptColor { get; init; }
    public HsvColorRange? SellButtonColor { get; init; }
    public Dictionary<AquariumActionKind, AquariumButtonCalibration> AquariumButtons { get; init; } = [];
    public double MinimumBlobArea { get; init; } = 5;
    public double MinimumConfidence { get; init; } = 0.60;

    public bool IsFishingCalibrated => FishMarkerColor.HasValue
        && ControlMarkerColor.HasValue
        && FishingRegionCalibrated;
    public bool IsCastCalibrated => CastIndicatorColor.HasValue
        && CastTargetColor.HasValue
        && CastRegionCalibrated;
    public bool IsShakeCalibrated => ShakePromptColor.HasValue
        && ShakeRegionCalibrated;
    public bool IsSellButtonCalibrated => SellButtonColor.HasValue && SellButtonRegionCalibrated;
    public bool IsInventoryVerificationCalibrated => InventoryItemsRegionCalibrated;
    public bool IsCatchNotificationCalibrated => CatchNotificationRegionCalibrated;
    public bool IsReconnectButtonCalibrated => ReconnectButtonRegionCalibrated && ReconnectButtonColor.HasValue;

    public void Validate()
    {
        FishingRegion.Validate();
        CastRegion.Validate();
        ShakeRegion.Validate();
        SellButtonRegion.Validate();
        InventoryItemsRegion.Validate();
        CatchNotificationRegion.Validate();
        ReconnectButtonRegion.Validate();

        if (ReferenceWidth < 0 || ReferenceHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ReferenceWidth));
        }

        if (!double.IsFinite(MinimumBlobArea) || MinimumBlobArea < 1 || MinimumBlobArea > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumBlobArea));
        }

        if (!double.IsFinite(MinimumConfidence) || MinimumConfidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumConfidence));
        }

        FishMarkerColor?.Validate();
        ControlMarkerColor?.Validate();
        CastIndicatorColor?.Validate();
        CastTargetColor?.Validate();
        ShakePromptColor?.Validate();
        SellButtonColor?.Validate();
        ReconnectButtonColor?.Validate();
        ArgumentNullException.ThrowIfNull(AquariumButtons);
        foreach (var button in AquariumButtons.Values)
            button.Validate();
    }
}
