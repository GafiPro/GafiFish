using System.Runtime.InteropServices;
using GafiFish.Core.Automation;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

public sealed record DayNightPhaseObservation(
    DayNightPhase Phase,
    double Confidence,
    string Status);

/// <summary>
/// Detects the day/night phase from one small region and two separately sampled HSV colours.
/// The detector never assumes a clock, game cycle duration, or UI position.
/// </summary>
public sealed class WorldPhaseVisionDetector
{
    public DayNightPhaseObservation Detect(ScreenCaptureFrame frame, VisionProfile profile)
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

        if (!profile.IsWorldPhaseCalibrated)
        {
            return new DayNightPhaseObservation(
                DayNightPhase.Unknown, 0,
                "Day/night phase indicator requires a calibrated region and both colours.");
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.WorldPhaseRegion.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);

        var day = FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y, profile.DayPhaseColor!.Value, profile.MinimumBlobArea);
        var night = FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y, profile.NightPhaseColor!.Value, profile.MinimumBlobArea);

        if (day is not null && night is not null)
        {
            return new DayNightPhaseObservation(
                DayNightPhase.Unknown, 0,
                "Day/night indicator is ambiguous: both calibrated colours were detected.");
        }

        if (day is not null)
        {
            return new DayNightPhaseObservation(
                DayNightPhase.Day, day.Confidence,
                $"Day indicator detected with {day.Confidence:P0} confidence.");
        }

        if (night is not null)
        {
            return new DayNightPhaseObservation(
                DayNightPhase.Night, night.Confidence,
                $"Night indicator detected with {night.Confidence:P0} confidence.");
        }

        return new DayNightPhaseObservation(
            DayNightPhase.Unknown, 0,
            "No calibrated day/night indicator was detected in its selected region.");
    }
}
