using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Detects calibrated reconnect and in-game HUD markers. It only analyses pixels;
/// it never sends input. Positive session confirmation requires an element chosen
/// by the user that is visible after the game world has loaded.
/// </summary>
public sealed class ConnectionRecoveryVisionDetector
{
    public DetectedMarker? DetectReconnectButton(ScreenCaptureFrame frame, VisionProfile profile) =>
        DetectCalibratedMarker(
            frame, profile,
            profile?.ReconnectButtonRegion,
            profile?.ReconnectButtonColor,
            profile?.IsReconnectButtonCalibrated == true);

    public DetectedMarker? DetectSessionIndicator(ScreenCaptureFrame frame, VisionProfile profile) =>
        DetectCalibratedMarker(
            frame, profile,
            profile?.SessionIndicatorRegion,
            profile?.SessionIndicatorColor,
            profile?.IsSessionIndicatorCalibrated == true);

    private static DetectedMarker? DetectCalibratedMarker(
        ScreenCaptureFrame frame,
        VisionProfile profile,
        NormalizedRegion? calibratedRegion,
        HsvColorRange? calibratedColor,
        bool isCalibrated)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != frame.Stride * frame.Height)
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));

        if (!isCalibrated || calibratedRegion is null || calibratedColor is null)
            return null;

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = calibratedRegion.Value.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);
        return FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y, calibratedColor.Value, profile.MinimumBlobArea);
    }
}
