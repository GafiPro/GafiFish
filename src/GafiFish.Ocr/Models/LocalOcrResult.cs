using GafiFish.Vision.Calibration;

namespace GafiFish.Ocr.Models;

public sealed record LocalOcrResult(
    string Text,
    double Confidence,
    NormalizedRegion Region,
    DateTimeOffset CapturedAt)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
}
