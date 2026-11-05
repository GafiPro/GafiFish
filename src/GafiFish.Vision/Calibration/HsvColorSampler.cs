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

        var hue = (int)Math.Round(hueDegrees / 2d);
        var saturation = max == 0 ? 0 : (int)Math.Round(delta / max * 255);
        var value = (int)Math.Round(max * 255);

        return new HsvColorRange(
            Math.Max(0, hue - hueTolerance),
            Math.Max(0, saturation - saturationTolerance),
            Math.Max(0, value - valueTolerance),
            Math.Min(179, hue + hueTolerance),
            Math.Min(255, saturation + saturationTolerance),
            Math.Min(255, value + valueTolerance));
    }
}
