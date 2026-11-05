using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;

namespace GafiFish.Vision.Detection;

/// <summary>
/// Recognises the user-calibrated Sell Anywhere button and can compare the
/// inventory item area before and after a sale. A change is evidence of a UI
/// update, not proof of the exact amount sold; ambiguous results fail closed.
/// </summary>
public sealed class SellAnywhereVisionDetector
{
    public SellAnywhereVisionResult DetectButton(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        if (!profile.IsSellButtonCalibrated)
        {
            return new SellAnywhereVisionResult(
                frame.CapturedAt, null, false,
                "Calibrate the Sell Anywhere button color first.");
        }

        ValidateFrame(frame);

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        using var hsv = new Mat();
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        var region = profile.SellButtonRegion.ToPixelRect(frame.Width, frame.Height);
        using var roi = new Mat(hsv, region);

        var button = FishingVisionDetector.FindLargestMarker(
            roi, region.X, region.Y,
            profile.SellButtonColor!.Value,
            profile.MinimumBlobArea);

        return new SellAnywhereVisionResult(
            frame.CapturedAt,
            button,
            true,
            button is null
                ? "Sell Anywhere button not found in the calibrated region."
                : "Sell Anywhere button candidate detected.");
    }

    /// <summary>
    /// Compares the inventory item list area between two frames.
    /// Returns a changed-pixel ratio between 0 and 1.
    /// </summary>
    public double MeasureInventoryChange(
        ScreenCaptureFrame before,
        ScreenCaptureFrame after,
        NormalizedRegion inventoryItemsRegion,
        int pixelDifferenceThreshold = 36)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        ValidateFrame(before);
        ValidateFrame(after);

        if (before.Width != after.Width || before.Height != after.Height)
        {
            throw new ArgumentException("Frames must have identical dimensions.");
        }

        if (pixelDifferenceThreshold is < 1 or > 765)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelDifferenceThreshold));
        }

        var region = inventoryItemsRegion.ToPixelRect(before.Width, before.Height);
        var changed = 0;
        var sampled = 0;

        // Sample a 4x4 grid per block to keep the check inexpensive.
        for (var y = region.Y; y < region.Bottom; y += 4)
        {
            for (var x = region.X; x < region.Right; x += 4)
            {
                var index = y * before.Stride + x * 4;
                var difference =
                    Math.Abs(before.BgraPixels[index] - after.BgraPixels[index])
                    + Math.Abs(before.BgraPixels[index + 1] - after.BgraPixels[index + 1])
                    + Math.Abs(before.BgraPixels[index + 2] - after.BgraPixels[index + 2]);

                if (difference >= pixelDifferenceThreshold)
                {
                    changed++;
                }

                sampled++;
            }
        }

        return sampled == 0 ? 0 : changed / (double)sampled;
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
