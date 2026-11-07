namespace GafiFish.Vision.Detection;

public sealed record RodChargeMeterObservation(
    DateTimeOffset CapturedAt,
    double Progress,
    double Confidence,
    string Status)
{
    public bool IsUsable => double.IsFinite(Progress)
        && Progress is >= 0 and <= 1
        && double.IsFinite(Confidence)
        && Confidence >= 0.55;
}
