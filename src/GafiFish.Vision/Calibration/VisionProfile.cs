using GafiFish.Core.Aquarium;
using GafiFish.Core.Automation;
using GafiFish.Core.Control;
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
    public NormalizedRegion SessionIndicatorRegion { get; init; } = NormalizedRegion.FullFrame;
    public bool SessionIndicatorRegionCalibrated { get; init; }
    public HsvColorRange? SessionIndicatorColor { get; init; }
    public NormalizedRegion WorldPhaseRegion { get; init; } = NormalizedRegion.FullFrame;
    public bool WorldPhaseRegionCalibrated { get; init; }
    public NormalizedRegion HuntSignalRegion { get; init; } = NormalizedRegion.FullFrame;
    public bool HuntSignalRegionCalibrated { get; init; }
    public NormalizedRegion RodNameRegion { get; init; } = NormalizedRegion.FullFrame;
    public bool RodNameRegionCalibrated { get; init; }
    public RodReelPreset RodPreset { get; init; } = RodReelPreset.Balanced;
    public RodMinigameMode RodMinigameMode { get; init; } = RodMinigameMode.StandardReel;
    public NormalizedRegion RodChargeMeterRegion { get; init; } = NormalizedRegion.FullFrame;
    public bool RodChargeMeterRegionCalibrated { get; init; }
    public HsvColorRange? RodChargeMeterColor { get; init; }
    public bool RodChargeMeterFillFromRight { get; init; }
    public double RodChargeReleaseThreshold { get; init; } = 0.80;
    public int JinglestarHoldMilliseconds { get; init; } = 100;
    public int JinglestarReleaseMilliseconds { get; init; } = 100;
    public bool JinglestarTimingCalibrated { get; init; }
    // Tranquility Rod uses four calibrated note lanes and one horizontal hit line.
    // Empty lane colours keep the mode disabled until the user calibrates real frames.
    public HsvColorRange?[] RhythmLaneColors { get; init; } = [null, null, null, null];
    public double RhythmHitLinePosition { get; init; } = 0.85;
    public bool RhythmHitLineCalibrated { get; init; }
    public double RhythmInputLeadMilliseconds { get; init; } = 60;
    public HsvColorRange? DayPhaseColor { get; init; }
    public HsvColorRange? NightPhaseColor { get; init; }
    public HsvColorRange? HuntSignalColor { get; init; }
    public bool FishingRegionCalibrated { get; init; }
    public bool CastRegionCalibrated { get; init; }
    public bool ShakeRegionCalibrated { get; init; }
    public bool SellButtonRegionCalibrated { get; init; }
    public bool InventoryItemsRegionCalibrated { get; init; }
    public HsvColorRange? FishMarkerColor { get; init; }
    public HsvColorRange? SecondaryFishMarkerColor { get; init; }
    public HsvColorRange? ControlMarkerColor { get; init; }
    public byte[]? FishMarkerTemplatePng { get; init; }
    public byte[]? ControlMarkerTemplatePng { get; init; }
    public HsvColorRange? CastIndicatorColor { get; init; }
    public HsvColorRange? CastTargetColor { get; init; }
    public HsvColorRange? ShakePromptColor { get; init; }
    public HsvColorRange? SellButtonColor { get; init; }
    public Dictionary<AquariumActionKind, AquariumButtonCalibration> AquariumButtons { get; init; } = [];
    public AquariumPanelCalibration AquariumPanel { get; init; } = new();
    public Dictionary<AnglerQuestButtonKind, AnglerQuestButtonCalibration> AnglerQuestButtons { get; init; } = [];
    public bool IsAnglerAcceptButtonCalibrated =>
        AnglerQuestButtons.TryGetValue(AnglerQuestButtonKind.AcceptRequest, out var accept) && accept.IsCalibrated;
    public bool IsAnglerSubmitButtonCalibrated =>
        AnglerQuestButtons.TryGetValue(AnglerQuestButtonKind.SubmitFish, out var submit) && submit.IsCalibrated;
    public double MinimumBlobArea { get; init; } = 5;
    public double MinimumConfidence { get; init; } = 0.60;

    public bool IsFishingCalibrated => FishingRegionCalibrated
        && (FishMarkerColor.HasValue || HasTemplate(FishMarkerTemplatePng))
        && (ControlMarkerColor.HasValue || HasTemplate(ControlMarkerTemplatePng));

    private static bool HasTemplate(byte[]? template) => template is { Length: >= 8 };
    public bool IsCastCalibrated => CastIndicatorColor.HasValue
        && CastTargetColor.HasValue
        && CastRegionCalibrated;
    public bool IsShakeCalibrated => ShakePromptColor.HasValue
        && ShakeRegionCalibrated;
    public bool IsSellButtonCalibrated => SellButtonColor.HasValue && SellButtonRegionCalibrated;
    public bool IsInventoryVerificationCalibrated => InventoryItemsRegionCalibrated;
    public bool IsCatchNotificationCalibrated => CatchNotificationRegionCalibrated;
    public bool IsReconnectButtonCalibrated => ReconnectButtonRegionCalibrated && ReconnectButtonColor.HasValue;
    public bool IsSessionIndicatorCalibrated => SessionIndicatorRegionCalibrated && SessionIndicatorColor.HasValue;
    public bool IsWorldPhaseCalibrated => WorldPhaseRegionCalibrated
        && DayPhaseColor.HasValue
        && NightPhaseColor.HasValue;
    public bool IsHuntSignalCalibrated => HuntSignalRegionCalibrated && HuntSignalColor.HasValue;
    public bool IsAquariumPanelCalibrated => AquariumPanel?.IsCalibrated == true;
    public bool IsRodChargeMeterCalibrated =>
        RodChargeMeterRegionCalibrated && RodChargeMeterColor.HasValue;
    public bool IsDualFishCalibrated =>
        FishingRegionCalibrated && FishMarkerColor.HasValue
        && SecondaryFishMarkerColor.HasValue && ControlMarkerColor.HasValue;
    public bool IsTranquilityRhythmCalibrated =>
        FishingRegionCalibrated
        && RhythmHitLineCalibrated
        && RhythmLaneColors is { Length: 4 }
        && RhythmLaneColors[0].HasValue
        && RhythmLaneColors[1].HasValue
        && RhythmLaneColors[2].HasValue
        && RhythmLaneColors[3].HasValue;

    public void Validate()
    {
        FishingRegion.Validate();
        CastRegion.Validate();
        ShakeRegion.Validate();
        SellButtonRegion.Validate();
        InventoryItemsRegion.Validate();
        CatchNotificationRegion.Validate();
        ReconnectButtonRegion.Validate();
        SessionIndicatorRegion.Validate();
        WorldPhaseRegion.Validate();
        HuntSignalRegion.Validate();
        RodNameRegion.Validate();
        if (!Enum.IsDefined(RodPreset))
            throw new ArgumentOutOfRangeException(nameof(RodPreset));
        if (!Enum.IsDefined(RodMinigameMode))
            throw new ArgumentOutOfRangeException(nameof(RodMinigameMode));
        RodChargeMeterRegion.Validate();
        if (JinglestarHoldMilliseconds is < 20 or > 500)
            throw new ArgumentOutOfRangeException(nameof(JinglestarHoldMilliseconds), "Jinglestar hold timing must be between 20 and 500 ms.");
        if (JinglestarReleaseMilliseconds is < 20 or > 500)
            throw new ArgumentOutOfRangeException(nameof(JinglestarReleaseMilliseconds), "Jinglestar release timing must be between 20 and 500 ms.");
        if (!double.IsFinite(RodChargeReleaseThreshold)
            || RodChargeReleaseThreshold is < 0.80 or > 1.00)
            throw new ArgumentOutOfRangeException(nameof(RodChargeReleaseThreshold),
                "Charge release threshold must be between 80% and 100%.");

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
        SecondaryFishMarkerColor?.Validate();
        ControlMarkerColor?.Validate();
        ValidateTemplate(FishMarkerTemplatePng, nameof(FishMarkerTemplatePng));
        ValidateTemplate(ControlMarkerTemplatePng, nameof(ControlMarkerTemplatePng));
        CastIndicatorColor?.Validate();
        CastTargetColor?.Validate();
        ShakePromptColor?.Validate();
        SellButtonColor?.Validate();
        ReconnectButtonColor?.Validate();
        SessionIndicatorColor?.Validate();
        DayPhaseColor?.Validate();
        NightPhaseColor?.Validate();
        HuntSignalColor?.Validate();
        RodChargeMeterColor?.Validate();
        ArgumentNullException.ThrowIfNull(RhythmLaneColors);
        if (RhythmLaneColors.Length != 4)
            throw new ArgumentException("Tranquility Rhythm requires exactly four lane colours.", nameof(RhythmLaneColors));
        foreach (var laneColor in RhythmLaneColors)
            laneColor?.Validate();
        if (!double.IsFinite(RhythmHitLinePosition) || RhythmHitLinePosition is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(RhythmHitLinePosition));
        if (!double.IsFinite(RhythmInputLeadMilliseconds)
            || RhythmInputLeadMilliseconds is < 0 or > 250)
            throw new ArgumentOutOfRangeException(nameof(RhythmInputLeadMilliseconds));
        ArgumentNullException.ThrowIfNull(AquariumButtons);
        foreach (var button in AquariumButtons.Values)
            button.Validate();
        ArgumentNullException.ThrowIfNull(AquariumPanel);
        AquariumPanel.Validate();

        ArgumentNullException.ThrowIfNull(AnglerQuestButtons);
        foreach (var button in AnglerQuestButtons.Values)
            button.Validate();
    }

    private static void ValidateTemplate(byte[]? template, string propertyName)
    {
        if (template is null)
            return;
        if (template.Length > 131_072)
            throw new ArgumentOutOfRangeException(propertyName, "Marker templates must not exceed 128 KiB.");
        if (template.Length == 0)
            return;

        ReadOnlySpan<byte> pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (template.Length < pngSignature.Length
            || !template.AsSpan(0, pngSignature.Length).SequenceEqual(pngSignature))
            throw new ArgumentException("Marker templates must be valid PNG data.", propertyName);
    }
}
