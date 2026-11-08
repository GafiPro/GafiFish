using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Reads a user-calibrated horizontal charge fill. It never assumes a screen
/// location or colour and returns no observation when the fill cannot be
/// distinguished from the background.
/// </summary>
public sealed class RodChargeMeterVisionDetector
{
    public RodChargeMeterObservation? Detect(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        if (!profile.IsRodChargeMeterCalibrated)
            return null;

        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != frame.Stride * frame.Height)
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.RodChargeMeterRegion.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);
        using var mask = new Mat();
        profile.RodChargeMeterColor!.Value.ApplyMask(roi, mask);

        var rowProgress = new List<double>();
        var validRows = 0;
        for (var y = 0; y < mask.Rows; y++)
        {
            var start = profile.RodChargeMeterFillFromRight ? mask.Cols - 1 : 0;
            var step = profile.RodChargeMeterFillFromRight ? -1 : 1;
            var run = 0;
            var gap = 0;
            var started = false;

            for (var offset = 0; offset < mask.Cols; offset++)
            {
                var x = start + (offset * step);
                var pixel = mask.At<byte>(y, x) != 0;
                if (pixel)
                {
                    started = true;
                    run++;
                    gap = 0;
                }
                else if (started)
                {
                    gap++;
                    if (gap > 2)
                        break;
                }
            }

            if (run >= 2)
            {
                rowProgress.Add(Math.Clamp(run / (double)mask.Cols, 0, 1));
                validRows++;
            }
        }

        if (rowProgress.Count == 0)
            return new RodChargeMeterObservation(
                frame.CapturedAt, 0, 0,
                "Charge meter fill not found in the calibrated region.");

        rowProgress.Sort();
        var progress = rowProgress[rowProgress.Count / 2];
        var rowCoverage = validRows / (double)Math.Max(1, mask.Rows);

        // A real fill should occupy several adjacent scan lines and have a
        // reasonably consistent edge. Isolated coloured pixels or reflections
        // must not look like a reliable charge reading.
        var deviations = rowProgress
            .Select(value => Math.Abs(value - progress))
            .OrderBy(value => value)
            .ToArray();
        var medianDeviation = deviations[deviations.Length / 2];
        var coverageConfidence = Math.Clamp(rowCoverage * 2.0, 0, 1);
        var consistencyConfidence = Math.Clamp(1.0 - medianDeviation * 3.0, 0.25, 1.0);
        var confidence = coverageConfidence * consistencyConfidence;

        return new RodChargeMeterObservation(
            frame.CapturedAt,
            progress,
            confidence,
            $"Charge meter {progress:P0} · confidence {confidence:P0} · {validRows}/{mask.Rows} scan lines.");
    }
}
