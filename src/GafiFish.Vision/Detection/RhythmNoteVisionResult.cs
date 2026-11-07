namespace GafiFish.Vision.Detection;

/// <summary>
/// One observation of the four Tranquility rhythm lanes. Lane order is D, F, J, K.
/// </summary>
public sealed record RhythmNoteVisionResult(
    DateTimeOffset CapturedAt,
    int FrameWidth,
    int FrameHeight,
    IReadOnlyList<DetectedMarker?> LaneMarkers,
    double HitLineY,
    bool IsCalibrated,
    string Status)
{
    public int DetectedLaneCount
    {
        get
        {
            var count = 0;
            foreach (var marker in LaneMarkers)
            {
                if (marker is not null)
                    count++;
            }

            return count;
        }
    }

    public bool HasNotes => DetectedLaneCount > 0;
}
