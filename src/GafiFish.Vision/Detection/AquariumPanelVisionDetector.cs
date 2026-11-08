using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Checks whether a calibrated colour patch belonging to the open Aquarium panel
/// is visible. It is a fail-closed guard, not a game-state or button detector.
/// </summary>
public sealed class AquariumPanelVisionDetector
{
    public AquariumPanelObservation Detect(ScreenCaptureFrame frame, VisionProfile profile)
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

        var calibration = profile.AquariumPanel;
        if (!calibration.IsCalibrated)
        {
            return new AquariumPanelObservation(
                frame.CapturedAt, false, 0,
                "Aquarium panel anchor is not calibrated. Select a small panel-only area and sample its colour.");
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = calibration.Region.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);
        using var mask = new Mat();
        calibration.PanelColor!.Value.ApplyMask(roi, mask);

        var totalPixels = Math.Max(1, region.Width * region.Height);
        var matchRatio = Cv2.CountNonZero(mask) / (double)totalPixels;
        var visible = double.IsFinite(matchRatio) && matchRatio >= calibration.MinimumMatchRatio;
        var status = visible
            ? $"Aquarium panel anchor confirmed ({matchRatio:P0} colour match)."
            : $"Aquarium panel not detected ({matchRatio:P0} match; requires {calibration.MinimumMatchRatio:P0}). No action was attempted.";

        return new AquariumPanelObservation(frame.CapturedAt, visible, matchRatio, status);
    }
}
