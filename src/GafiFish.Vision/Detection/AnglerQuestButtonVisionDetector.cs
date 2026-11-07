using System.Runtime.InteropServices;
using GafiFish.Core.Automation;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Detects an Angler dialogue button only inside the region and colour
/// sampled by the user. This detector never sends input.
/// </summary>
public sealed class AnglerQuestButtonVisionDetector
{
    public DetectedMarker? Detect(
        ScreenCaptureFrame frame,
        VisionProfile profile,
        AnglerQuestButtonKind kind)
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

        if (!profile.AnglerQuestButtons.TryGetValue(kind, out var calibration)
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
