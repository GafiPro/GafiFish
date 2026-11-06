using System.Runtime.InteropServices;
using GafiFish.Core.Aquarium;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Finds a user-calibrated, enabled Aquarium action button in its own region.
/// It only detects pixels; it does not send input or infer game state.
/// </summary>
public sealed class AquariumButtonVisionDetector
{
    public DetectedMarker? Detect(
        ScreenCaptureFrame frame,
        VisionProfile profile,
        AquariumActionKind action)
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

        if (!profile.AquariumButtons.TryGetValue(action, out var calibration)
            || !calibration.IsCalibrated)
        {
            return null;
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = calibration.Region.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);

        return FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y, calibration.ButtonColor!.Value, profile.MinimumBlobArea);
    }
}
