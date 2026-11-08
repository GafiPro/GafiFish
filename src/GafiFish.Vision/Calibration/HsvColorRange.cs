using OpenCvSharp;

namespace GafiFish.Vision.Calibration;

/// <summary>
/// OpenCV HSV range. Hue uses OpenCV's circular 0..179 scale; saturation/value use 0..255.
/// HueMinimum greater than HueMaximum represents a range crossing the red 179/0 boundary.
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
            || SaturationMinimum > SaturationMaximum
            || ValueMinimum > ValueMaximum)
        {
            throw new ArgumentOutOfRangeException(nameof(HsvColorRange), "Invalid HSV bounds.");
        }
    }

    /// <summary>
    /// Applies this colour range to an OpenCV HSV image. Wrapped hue intervals
    /// are split at 179/0 and ORed so red pixels on either side remain detectable.
    /// </summary>
    public void ApplyMask(Mat hsvImage, Mat destination)
    {
        ArgumentNullException.ThrowIfNull(hsvImage);
        ArgumentNullException.ThrowIfNull(destination);
        Validate();

        if (HueMinimum <= HueMaximum)
        {
            Cv2.InRange(hsvImage, Lower, Upper, destination);
            return;
        }

        using var highHueMask = new Mat();
        using var lowHueMask = new Mat();
        Cv2.InRange(
            hsvImage,
            new Scalar(HueMinimum, SaturationMinimum, ValueMinimum),
            new Scalar(179, SaturationMaximum, ValueMaximum),
            highHueMask);
        Cv2.InRange(
            hsvImage,
            new Scalar(0, SaturationMinimum, ValueMinimum),
            new Scalar(HueMaximum, SaturationMaximum, ValueMaximum),
            lowHueMask);
        Cv2.BitwiseOr(highHueMask, lowHueMask, destination);
    }

    public Scalar Lower => new(HueMinimum, SaturationMinimum, ValueMinimum);
    public Scalar Upper => new(HueMaximum, SaturationMaximum, ValueMaximum);
}
