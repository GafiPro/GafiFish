using OpenCvSharp;

namespace GafiFish.Vision.Calibration;

/// <summary>
/// OpenCV HSV range. Hue uses OpenCV's 0..179 scale; saturation/value use 0..255.
/// </summary>
public readonly record struct HsvColorRange(
    int HueMinimum,
    int SaturationMinimum,
    int ValueMinimum,
    int HueMaximum,
    int SaturationMaximum,
    int ValueMaximum)
{
    public void Validate()
    {
        if (HueMinimum is < 0 or > 179 || HueMaximum is < 0 or > 179
            || SaturationMinimum is < 0 or > 255 || SaturationMaximum is < 0 or > 255
            || ValueMinimum is < 0 or > 255 || ValueMaximum is < 0 or > 255
            || HueMinimum > HueMaximum
            || SaturationMinimum > SaturationMaximum
            || ValueMinimum > ValueMaximum)
        {
            throw new ArgumentOutOfRangeException(nameof(HsvColorRange), "Invalid HSV bounds.");
        }
    }

    public Scalar Lower => new(HueMinimum, SaturationMinimum, ValueMinimum);
    public Scalar Upper => new(HueMaximum, SaturationMaximum, ValueMaximum);
}
