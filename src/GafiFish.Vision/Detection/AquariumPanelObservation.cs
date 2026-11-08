namespace GafiFish.Vision.Detection;

/// <summary>
/// Result of checking the user-calibrated visual anchor for the open Aquarium panel.
/// </summary>
public sealed record AquariumPanelObservation(
    DateTimeOffset CapturedAt,
    bool IsVisible,
    double MatchRatio,
    string Status);
