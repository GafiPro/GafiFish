using OpenCvSharp;

namespace GafiFish.Vision.Detection;

public sealed record DetectedMarker(
    double CenterX,
    double CenterY,
    Rect Bounds,
    double Area,
    double Confidence);
