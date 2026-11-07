using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

public sealed record HuntSignalObservation(
    bool IsDetected,
    double Confidence,
    string Status,
    DateTimeOffset CapturedAt);

/// <summary>
/// Read-only hunt cue detector. It looks only inside a region and HSV colour
/// explicitly calibrated by the user; it never sends input or infers a location.
/// </summary>
public sealed class HuntSignalVisionDetector
{
    public HuntSignalObservation Detect(ScreenCaptureFrame frame, VisionProfile profile)
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

        if (!profile.IsHuntSignalCalibrated)
        {
            return new HuntSignalObservation(
                false, 0,
                "Hunt alerts need a calibrated cue region and colour.",
                frame.CapturedAt);
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.HuntSignalRegion.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);
        var marker = FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y, profile.HuntSignalColor!.Value, profile.MinimumBlobArea);

        var detected = marker is not null && marker.Confidence >= profile.MinimumConfidence;
        var confidence = detected ? marker!.Confidence : 0d;
        var status = detected
            ? $"Calibrated hunt cue detected ({confidence:P0} confidence)."
            : "No calibrated hunt cue detected in the selected region.";

        return new HuntSignalObservation(detected, confidence, status, frame.CapturedAt);
    }
}
