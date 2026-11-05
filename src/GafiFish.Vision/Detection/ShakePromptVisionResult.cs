namespace GafiFish.Vision.Detection;

public sealed record ShakePromptVisionResult(
    DateTimeOffset CapturedAt,
    int FrameWidth,
    int FrameHeight,
    IReadOnlyList<DetectedMarker> Prompts,
    bool IsCalibrated,
    string Status);
