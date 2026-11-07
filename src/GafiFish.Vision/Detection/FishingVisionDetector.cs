using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Detects calibrated fish and control markers from visible BGRA screen frames.
/// The detector never sends input and never accesses the Roblox process.
/// </summary>
public sealed class FishingVisionDetector
{
    public FishingVisionResult Detect(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != frame.Stride * frame.Height)
        {
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));
        }

        if (!profile.IsFishingCalibrated)
        {
            return new FishingVisionResult(
                frame.CapturedAt, frame.Width, frame.Height,
                null, null, false,
                "Calibrate the fishing region and provide a colour sample or template for both markers.");
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);

        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);

        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.FishingRegion.ToPixelRect(frame.Width, frame.Height);
        using var hsvRoi = new Mat(hsv, region);
        using var bgrRoi = new Mat(bgr, region);

        var fish = profile.FishMarkerColor.HasValue
            ? FindLargestMarker(
                hsvRoi, region.X, region.Y, profile.FishMarkerColor.Value, profile.MinimumBlobArea)
            : null;
        if ((fish is null || fish.Confidence < profile.MinimumConfidence)
            && profile.FishMarkerTemplatePng is { Length: > 0 })
        {
            fish = FindTemplateMarker(
                bgrRoi, region.X, region.Y,
                profile.FishMarkerTemplatePng,
                frame.Width, frame.Height,
                profile.ReferenceWidth, profile.ReferenceHeight) ?? fish;
        }

        var control = profile.ControlMarkerColor.HasValue
            ? FindLargestMarker(
                hsvRoi, region.X, region.Y, profile.ControlMarkerColor.Value, profile.MinimumBlobArea)
            : null;
        if ((control is null || control.Confidence < profile.MinimumConfidence)
            && profile.ControlMarkerTemplatePng is { Length: > 0 })
        {
            control = FindTemplateMarker(
                bgrRoi, region.X, region.Y,
                profile.ControlMarkerTemplatePng,
                frame.Width, frame.Height,
                profile.ReferenceWidth, profile.ReferenceHeight) ?? control;
        }

        var enoughConfidence =
            fish is not null && control is not null
            && fish.Confidence >= profile.MinimumConfidence
            && control.Confidence >= profile.MinimumConfidence;

        var status = enoughConfidence
            ? "Both markers detected."
            : fish is null && control is null
                ? "No calibrated markers found in the search region."
                : "Only one marker found or detection confidence is low.";

        return new FishingVisionResult(
            frame.CapturedAt, frame.Width, frame.Height,
            fish, control, true, status);
    }

    private static DetectedMarker? FindTemplateMarker(
        Mat bgrRegion,
        int offsetX,
        int offsetY,
        byte[] templatePng,
        int frameWidth,
        int frameHeight,
        int referenceWidth,
        int referenceHeight)
    {
        using var template = Cv2.ImDecode(templatePng, ImreadModes.Grayscale);
        if (template.Empty() || template.Width < 3 || template.Height < 3)
            return null;

        var scaleX = referenceWidth > 0 ? frameWidth / (double)referenceWidth : 1d;
        var scaleY = referenceHeight > 0 ? frameHeight / (double)referenceHeight : 1d;
        var targetWidth = Math.Max(3, (int)Math.Round(template.Width * scaleX));
        var targetHeight = Math.Max(3, (int)Math.Round(template.Height * scaleY));

        using var resized = new Mat();
        var matchTemplate = template;
        if (targetWidth != template.Width || targetHeight != template.Height)
        {
            Cv2.Resize(
                template, resized, new Size(targetWidth, targetHeight),
                interpolation: InterpolationFlags.Cubic);
            matchTemplate = resized;
        }

        using var grayRegion = new Mat();
        Cv2.CvtColor(bgrRegion, grayRegion, ColorConversionCodes.BGR2GRAY);
        if (matchTemplate.Width > grayRegion.Width || matchTemplate.Height > grayRegion.Height)
            return null;

        using var result = new Mat();
        Cv2.MatchTemplate(grayRegion, matchTemplate, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(
            result,
            out _,
            out var maximumScore,
            out _,
            out var maximumLocation);

        if (!double.IsFinite(maximumScore) || maximumScore < 0.82)
            return null;

        var bounds = new Rect(
            offsetX + maximumLocation.X,
            offsetY + maximumLocation.Y,
            matchTemplate.Width,
            matchTemplate.Height);
        return new DetectedMarker(
            bounds.X + bounds.Width / 2d,
            bounds.Y + bounds.Height / 2d,
            bounds,
            bounds.Width * (double)bounds.Height,
            Math.Clamp(maximumScore, 0, 1));
    }

    internal static DetectedMarker? FindLargestMarker(
        Mat hsvRegion,
        int offsetX,
        int offsetY,
        HsvColorRange color,
        double minimumArea)
    {
        using var mask = new Mat();
        Cv2.InRange(hsvRegion, color.Lower, color.Upper, mask);

        using var kernel = Cv2.GetStructuringElement(
            MorphShapes.Ellipse, new Size(3, 3));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);

        Cv2.FindContours(
            mask,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var bestArea = 0d;
        Rect bestBounds = default;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            if (area < minimumArea || area <= bestArea)
            {
                continue;
            }

            bestArea = area;
            bestBounds = Cv2.BoundingRect(contour);
        }

        if (bestArea < minimumArea)
        {
            return null;
        }

        var bounds = new Rect(
            bestBounds.X + offsetX,
            bestBounds.Y + offsetY,
            bestBounds.Width,
            bestBounds.Height);

        var boxArea = Math.Max(1, bounds.Width * bounds.Height);
        var fillRatio = Math.Clamp(bestArea / boxArea, 0, 1);
        var areaScore = Math.Clamp(bestArea / (minimumArea * 8), 0, 1);
        var confidence = Math.Clamp(0.55 + 0.25 * areaScore + 0.20 * fillRatio, 0, 1);

        return new DetectedMarker(
            bounds.X + bounds.Width / 2d,
            bounds.Y + bounds.Height / 2d,
            bounds,
            bestArea,
            confidence);
    }
}
