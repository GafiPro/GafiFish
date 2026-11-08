using OpenCvSharp;

namespace GafiFish.Vision.Detection;

public sealed record DetectedMarker(
    double CenterX,
    double CenterY,
    Rect Bounds,
    double Area,
    double Confidence)
{
    /// <summary>
    /// Optional oriented bounds for controls that rotate. Bounds remains the
    /// axis-aligned box for overlays and compatibility with existing callers.
    /// </summary>
    public RotatedRect? OrientedBounds { get; init; }
}
