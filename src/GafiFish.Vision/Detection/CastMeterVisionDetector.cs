using System.Runtime.InteropServices;
using GafiFish.Core.Casting;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Reads the calibrated cast indicator and target zone from visible frames.
/// Returns null until both cast colors are calibrated or a marker is missing.
/// </summary>
public sealed class CastMeterVisionDetector
{
    private double? _previousIndicatorPosition;
    private DateTimeOffset _previousCapturedAt;

    public CastMeterObservation? Detect(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        if (!profile.IsCastCalibrated)
        {
            return null;
        }

        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != frame.Stride * frame.Height)
        {
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);

        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);

        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.CastRegion.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);

        var indicator = FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y, profile.CastIndicatorColor!.Value, profile.MinimumBlobArea);
        var target = FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y, profile.CastTargetColor!.Value, profile.MinimumBlobArea);

        if (indicator is null || target is null)
        {
            Reset();
            return null;
        }

        var indicatorPosition = (indicator.CenterX - region.X) / region.Width;
        var targetStart = (target.Bounds.X - region.X) / (double)region.Width;
        var targetEnd = (target.Bounds.Right - region.X) / (double)region.Width;

        if (!double.IsFinite(indicatorPosition)
            || !double.IsFinite(targetStart)
            || !double.IsFinite(targetEnd)
            || targetStart < 0 || targetEnd > 1 || targetStart >= targetEnd)
        {
            Reset();
            return null;
        }

        var velocity = 0d;
        if (_previousIndicatorPosition.HasValue)
        {
            var elapsed = (frame.CapturedAt - _previousCapturedAt).TotalSeconds;
            if (elapsed > 0 && elapsed <= 1)
            {
                velocity = (indicatorPosition - _previousIndicatorPosition.Value) / elapsed;
            }
        }

        _previousIndicatorPosition = indicatorPosition;
        _previousCapturedAt = frame.CapturedAt;

        return new CastMeterObservation(
            IsVisible: true,
            IndicatorPosition: indicatorPosition,
            TargetStart: targetStart,
            TargetEnd: targetEnd,
            IndicatorVelocityPerSecond: velocity,
            Confidence: Math.Min(indicator.Confidence, target.Confidence),
            CapturedAt: frame.CapturedAt);
    }

    public void Reset()
    {
        _previousIndicatorPosition = null;
        _previousCapturedAt = default;
    }
}
