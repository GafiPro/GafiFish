using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Detects the nearest falling note in each user-calibrated Tranquility lane.
/// It uses only the visible client frame and calibrated HSV ranges; it never
/// assumes screen coordinates or sends input.
/// </summary>
public sealed class RhythmNoteVisionDetector
{
    public RhythmNoteVisionResult Detect(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        ValidateFrame(frame);

        var empty = new DetectedMarker?[4];
        if (!profile.IsTranquilityRhythmCalibrated)
        {
            return new RhythmNoteVisionResult(
                frame.CapturedAt,
                frame.Width,
                frame.Height,
                empty,
                0,
                false,
                "Tranquility Rhythm needs the four lane colours, the hit line and the four-lane search region calibrated.");
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.FishingRegion.ToPixelRect(frame.Width, frame.Height);
        var hitLineY = region.Y + (profile.RhythmHitLinePosition * region.Height);
        var laneMarkers = new DetectedMarker?[4];

        for (var lane = 0; lane < 4; lane++)
        {
            var laneLeft = region.X + (int)Math.Floor(lane * region.Width / 4d);
            var laneRight = region.X + (int)Math.Floor((lane + 1) * region.Width / 4d);
            var laneWidth = Math.Max(1, laneRight - laneLeft);
            var laneRect = new Rect(laneLeft, region.Y, laneWidth, region.Height);

            using var laneHsv = new Mat(hsv, laneRect);
            using var mask = new Mat();
            var colour = profile.RhythmLaneColors[lane]!.Value;
            colour.ApplyMask(laneHsv, mask);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
            Cv2.FindContours(
                mask,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            DetectedMarker? best = null;
            foreach (var contour in contours)
            {
                var area = Cv2.ContourArea(contour);
                if (!double.IsFinite(area) || area < profile.MinimumBlobArea)
                    continue;

                var localBounds = Cv2.BoundingRect(contour);
                if (localBounds.Width < 4 || localBounds.Height < 4
                    || localBounds.Width > laneWidth * 0.85)
                    continue;

                var aspect = localBounds.Width / (double)localBounds.Height;
                if (aspect is < 0.40 or > 2.50)
                    continue;

                var bounds = new Rect(
                    laneLeft + localBounds.X,
                    region.Y + localBounds.Y,
                    localBounds.Width,
                    localBounds.Height);
                var centerX = bounds.X + (bounds.Width / 2d);
                var centerY = bounds.Y + (bounds.Height / 2d);

                // The stationary receptor sits on the hit line. Ignore it and
                // only track notes approaching from above the calibrated line.
                var receptorClearance = Math.Max(3, bounds.Height * 0.35);
                if (centerY >= hitLineY - receptorClearance)
                    continue;

                var boxArea = Math.Max(1, bounds.Width * bounds.Height);
                var fillRatio = Math.Clamp(area / boxArea, 0, 1);
                var areaScore = Math.Clamp(area / (profile.MinimumBlobArea * 8), 0, 1);
                var confidence = Math.Clamp(0.55 + (0.25 * areaScore) + (0.20 * fillRatio), 0, 1);
                var candidate = new DetectedMarker(centerX, centerY, bounds, area, confidence);

                // There can be more than one note in a lane. Track the one
                // closest to the receptor because it is the next actionable note.
                if (best is null || candidate.CenterY > best.CenterY)
                    best = candidate;
            }

            if (best is not null && best.Confidence >= profile.MinimumConfidence)
                laneMarkers[lane] = best;
        }

        var detected = 0;
        foreach (var marker in laneMarkers)
        {
            if (marker is not null)
                detected++;
        }

        return new RhythmNoteVisionResult(
            frame.CapturedAt,
            frame.Width,
            frame.Height,
            laneMarkers,
            hitLineY,
            true,
            detected == 0
                ? "Tranquility Rhythm calibrated · waiting for falling notes."
                : $"Tranquility Rhythm · {detected}/4 lanes contain a visible note.");
    }

    private static void ValidateFrame(ScreenCaptureFrame frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != frame.Stride * frame.Height)
        {
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));
        }
    }
}
