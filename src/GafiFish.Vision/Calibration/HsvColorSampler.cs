namespace GafiFish.Vision.Calibration;

/// <summary>
/// Converts a sampled BGRA pixel into a tolerant OpenCV HSV interval.
/// Intended for sampling clear, flat UI marker pixels from a captured frame.
/// </summary>
public static class HsvColorSampler
{
    public static HsvColorRange FromBgra(
        byte blue,
        byte green,
        byte red,
        int hueTolerance = 8,
        int saturationTolerance = 70,
        int valueTolerance = 70)
    {
        if (hueTolerance is < 0 or > 30
            || saturationTolerance is < 0 or > 255
            || valueTolerance is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(hueTolerance));
        }

        var b = blue / 255d;
        var g = green / 255d;
        var r = red / 255d;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var hueDegrees = delta == 0
            ? 0
            : max == r
                ? 60 * (((g - b) / delta) % 6)
                : max == g
                    ? 60 * (((b - r) / delta) + 2)
                    : 60 * (((r - g) / delta) + 4);

        if (hueDegrees < 0)
        {
            hueDegrees += 360;
        }

        // OpenCV hue is circular: 179 and 0 are adjacent reds. Preserve a
        // wrapped interval instead of clipping it at either end. For near-neutral
        // samples, hue is undefined, so accept the full hue range.
        var hue = (int)Math.Round(hueDegrees / 2d) % 180;
        var saturation = max == 0 ? 0 : (int)Math.Round(delta / max * 255);
        var value = (int)Math.Round(max * 255);
        var minimumHue = saturation < 12 ? 0 : (hue - hueTolerance + 180) % 180;
        var maximumHue = saturation < 12 ? 179 : (hue + hueTolerance) % 180;

        return new HsvColorRange(
            minimumHue,
            Math.Max(0, saturation - saturationTolerance),
            Math.Max(0, value - valueTolerance),
            maximumHue,
            Math.Min(255, saturation + saturationTolerance),
            Math.Min(255, value + valueTolerance));
    }
}
