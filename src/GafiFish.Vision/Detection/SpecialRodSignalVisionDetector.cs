using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

public sealed record SpecialRodSignalObservation(
    DateTimeOffset CapturedAt,
    bool IsCalibrated,
    IReadOnlyList<DetectedMarker> Markers,
    string Status);

/// <summary>
/// Read-only detector for special-rod visual cues. Every search region and HSV
/// colour must be sampled by the user; the detector never guesses game colours
/// or sends input.
/// </summary>
public sealed class SpecialRodSignalVisionDetector
{
    public SpecialRodSignalObservation DetectMiguRodHazards(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        return Detect(frame, profile, profile.MiguRodHazardRegion,
            profile.IsMiguRodHazardCalibrated, profile.MiguRodHazardColor, "MiguRod cut-warning");
    }

    public SpecialRodSignalObservation DetectDredBonusObjects(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        return Detect(frame, profile, profile.DredBonusObjectRegion,
            profile.IsDredBonusObjectCalibrated, profile.DredBonusObjectColor, "D.R.E.D. falling bonus-object");
    }

    private static SpecialRodSignalObservation Detect(
        ScreenCaptureFrame frame,
        VisionProfile profile,
        NormalizedRegion searchRegion,
        bool isCalibrated,
        HsvColorRange? sampledColor,
        string signalName)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        ValidateFrame(frame);

        if (!isCalibrated || !sampledColor.HasValue)
        {
            return new SpecialRodSignalObservation(frame.CapturedAt, false, [],
                $"{signalName} detection needs a selected region and a sampled colour.");
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = searchRegion.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);
        using var mask = new Mat();
        sampledColor.Value.ApplyMask(roi, mask);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.FindContours(mask, out Point[][] contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var markers = contours
            .Select(contour => new { Area = Cv2.ContourArea(contour), Bounds = Cv2.BoundingRect(contour) })
            .Where(item => item.Area >= profile.MinimumBlobArea
                && item.Bounds.Width >= 3 && item.Bounds.Height >= 3)
            .OrderByDescending(item => item.Area)
            .Take(8)
            .Select(item =>
            {
                var bounds = new Rect(region.X + item.Bounds.X, region.Y + item.Bounds.Y,
                    item.Bounds.Width, item.Bounds.Height);
                var boxArea = Math.Max(1, bounds.Width * bounds.Height);
                var fillRatio = Math.Clamp(item.Area / boxArea, 0, 1);
                var areaScore = Math.Clamp(item.Area / (profile.MinimumBlobArea * 8), 0, 1);
                var confidence = Math.Clamp(0.55 + 0.25 * areaScore + 0.20 * fillRatio, 0, 1);
                return new DetectedMarker(bounds.X + bounds.Width / 2d,
                    bounds.Y + bounds.Height / 2d, bounds, item.Area, confidence);
            })
            .Where(marker => marker.Confidence >= profile.MinimumConfidence)
            .ToArray();

        return new SpecialRodSignalObservation(frame.CapturedAt, true, markers,
            markers.Length == 0
                ? $"No calibrated {signalName} marker was detected."
                : $"Detected {markers.Length} calibrated {signalName} candidate(s).");
    }

    private static void ValidateFrame(ScreenCaptureFrame frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != checked(frame.Stride * frame.Height))
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));
    }
}