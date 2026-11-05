namespace GafiFish.Core.Casting;

/// <summary>
/// Normalised visual measurement from the cast-meter detector.
/// Positions are in the inclusive 0..1 range.
/// </summary>
public sealed record CastMeterObservation(
    bool IsVisible,
    double IndicatorPosition,
    double TargetStart,
    double TargetEnd,
    double IndicatorSpeedPerSecond,
    double Confidence,
    DateTimeOffset CapturedAt);
