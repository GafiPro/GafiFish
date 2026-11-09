using GafiFish.Windows.Capture;

namespace GafiFish.Vision.Calibration;

/// <summary>
/// Builds a conservative fishing calibration suggestion from two pixels selected
/// by the user in a visible frame. It never captures the screen or sends input.
/// </summary>
public static class FishingAutoCalibrator
{
    public static FishingAutoCalibrationSuggestion Create(
        ScreenCaptureFrame frame,
        int fishX,
        int fishY,
        int controlX,
        int controlY)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Width < 16 || frame.Height < 16
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != frame.Stride * frame.Height)
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));

        ValidatePoint(frame, fishX, fishY, nameof(fishX));
        ValidatePoint(frame, controlX, controlY, nameof(controlX));

        var dx = controlX - fishX;
        var dy = controlY - fishY;
        if ((dx * (double)dx) + (dy * (double)dy) < 144)
            throw new InvalidOperationException("The two sample points are too close. Select the centre of each different marker.");

        var fishRgb = SampleMedianBgra(frame, fishX, fishY);
        var controlRgb = SampleMedianBgra(frame, controlX, controlY);
        var colourDistance = Math.Sqrt(
            Math.Pow(fishRgb.Red - controlRgb.Red, 2)
            + Math.Pow(fishRgb.Green - controlRgb.Green, 2)
            + Math.Pow(fishRgb.Blue - controlRgb.Blue, 2));

        if (colourDistance < 45)
            throw new InvalidOperationException("The selected markers have very similar colours. Choose a clear, solid-colour part of each marker.");

        var fishColour = HsvColorSampler.FromBgra(
            fishRgb.Blue, fishRgb.Green, fishRgb.Red,
            hueTolerance: 6, saturationTolerance: 45, valueTolerance: 50);
        var controlColour = HsvColorSampler.FromBgra(
            controlRgb.Blue, controlRgb.Green, controlRgb.Red,
            hueTolerance: 6, saturationTolerance: 45, valueTolerance: 50);

        // The user only samples the markers; provide a generous surrounding
        // search area so the fish can move beyond the exact sample positions.
        var spanX = Math.Min(frame.Width,
            Math.Max(frame.Width * 0.34, Math.Abs(dx) + frame.Width * 0.14));
        var spanY = Math.Min(frame.Height,
            Math.Max(frame.Height * 0.14, Math.Abs(dy) + frame.Height * 0.08));
        var regionWidth = Math.Clamp((int)Math.Ceiling(spanX), 1, frame.Width);
        var regionHeight = Math.Clamp((int)Math.Ceiling(spanY), 1, frame.Height);
        var centreX = (fishX + controlX) / 2d;
        var centreY = (fishY + controlY) / 2d;
        var left = Math.Clamp(
            (int)Math.Floor(centreX - regionWidth / 2d), 0, frame.Width - regionWidth);
        var top = Math.Clamp(
            (int)Math.Floor(centreY - regionHeight / 2d), 0, frame.Height - regionHeight);

        var region = new NormalizedRegion(
            left / (double)frame.Width,
            top / (double)frame.Height,
            regionWidth / (double)frame.Width,
            regionHeight / (double)frame.Height);
        region.Validate();

        return new FishingAutoCalibrationSuggestion(region, fishColour, controlColour);
    }

    private static void ValidatePoint(ScreenCaptureFrame frame, int x, int y, string parameterName)
    {
        if (x < 0 || x >= frame.Width || y < 0 || y >= frame.Height)
            throw new ArgumentOutOfRangeException(parameterName, "Sample point must be inside the captured frame.");
    }

    private static SampledColour SampleMedianBgra(ScreenCaptureFrame frame, int x, int y)
    {
        const int radius = 2;
        var blue = new byte[25];
        var green = new byte[25];
        var red = new byte[25];
        var count = 0;

        for (var sampleY = Math.Max(0, y - radius); sampleY <= Math.Min(frame.Height - 1, y + radius); sampleY++)
        {
            for (var sampleX = Math.Max(0, x - radius); sampleX <= Math.Min(frame.Width - 1, x + radius); sampleX++)
            {
                var offset = sampleY * frame.Stride + sampleX * 4;
                blue[count] = frame.BgraPixels[offset];
                green[count] = frame.BgraPixels[offset + 1];
                red[count] = frame.BgraPixels[offset + 2];
                count++;
            }
        }

        Array.Sort(blue, 0, count);
        Array.Sort(green, 0, count);
        Array.Sort(red, 0, count);
        var middle = count / 2;
        return new SampledColour(blue[middle], green[middle], red[middle]);
    }

    private readonly record struct SampledColour(byte Blue, byte Green, byte Red);
}

public sealed record FishingAutoCalibrationSuggestion(
    NormalizedRegion SearchRegion,
    HsvColorRange FishMarkerColor,
    HsvColorRange ControlMarkerColor);
