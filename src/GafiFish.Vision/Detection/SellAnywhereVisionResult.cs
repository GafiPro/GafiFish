namespace GafiFish.Vision.Detection;

public sealed record SellAnywhereVisionResult(
    DateTimeOffset CapturedAt,
    DetectedMarker? SellButton,
    bool IsCalibrated,
    string Status);
