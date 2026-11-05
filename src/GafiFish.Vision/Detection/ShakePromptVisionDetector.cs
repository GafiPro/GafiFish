using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Finds compact, high-contrast prompt shapes in the calibrated Shake region.
/// It reports candidate centres only; it never clicks or sends input.
/// </summary>
public sealed class ShakePromptVisionDetector
{
    public ShakePromptVisionResult Detect(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        if (!profile.IsShakeCalibrated)
        {
            return new ShakePromptVisionResult(
                frame.CapturedAt, frame.Width, frame.Height,
                [], false, "Calibrate the Shake prompt color first.");
        }

        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != frame.Stride * frame.Height)
        {
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);

        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);

        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.ShakeRegion.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);
        using var mask = new Mat();
        Cv2.InRange(roi, profile.ShakePromptColor!.Value.Lower, profile.ShakePromptColor.Value.Upper, mask);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);

        Cv2.FindContours(
            mask,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var prompts = new List<DetectedMarker>();

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < profile.MinimumBlobArea)
            {
                continue;
            }

            var bounds = Cv2.BoundingRect(contour);
            if (bounds.Width < 3 || bounds.Height < 3)
            {
                continue;
            }

            var aspectRatio = bounds.Width / (double)bounds.Height;
            if (aspectRatio is < 0.45 or > 2.2)
            {
                continue;
            }

            var perimeter = Cv2.ArcLength(contour, closed: true);
            if (perimeter <= 0)
            {
                continue;
            }

            var circularity = Math.Clamp(4 * Math.PI * area / (perimeter * perimeter), 0, 1);
            var areaScore = Math.Clamp(area / (profile.MinimumBlobArea * 10), 0, 1);
            var shapeScore = Math.Clamp(circularity / 0.75, 0, 1);
            var confidence = Math.Clamp(0.45 + 0.30 * areaScore + 0.25 * shapeScore, 0, 1);

            prompts.Add(new DetectedMarker(
                region.X + bounds.X + bounds.Width / 2d,
                region.Y + bounds.Y + bounds.Height / 2d,
                new Rect(region.X + bounds.X, region.Y + bounds.Y, bounds.Width, bounds.Height),
                area,
                confidence));
        }

        var ordered = prompts
            .OrderByDescending(prompt => prompt.Confidence)
            .ThenByDescending(prompt => prompt.Area)
            .ToArray();

        return new ShakePromptVisionResult(
            frame.CapturedAt,
            frame.Width,
            frame.Height,
            ordered,
            true,
            ordered.Length == 0
                ? "No Shake prompt candidates detected."
                : $"{ordered.Length} Shake prompt candidate(s) detected.");
    }
}
