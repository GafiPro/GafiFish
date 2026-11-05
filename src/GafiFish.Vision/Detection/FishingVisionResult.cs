namespace GafiFish.Vision.Detection;

public sealed record FishingVisionResult(
    DateTimeOffset CapturedAt,
    int FrameWidth,
    int FrameHeight,
    DetectedMarker? FishMarker,
    DetectedMarker? ControlMarker,
    bool IsCalibrated,
    string Status);
