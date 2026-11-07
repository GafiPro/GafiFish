using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Detects only the reconnect button region and colour explicitly calibrated by the user.
/// This class detects pixels only; it never sends input or infers successful reconnection.
/// </summary>
public sealed class ConnectionRecoveryVisionDetector
{
    public DetectedMarker? DetectReconnectButton(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != frame.Stride * frame.Height)
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));

        if (!profile.IsReconnectButtonCalibrated)
            return null;

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.ReconnectButtonRegion.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);
        return FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y, profile.ReconnectButtonColor!.Value, profile.MinimumBlobArea);
    }
}
