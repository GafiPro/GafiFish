using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;

namespace GafiFish.Vision.Tests;

public sealed class HuntSignalVisionDetectorTests
{
    [Fact]
    public void ReturnsNotCalibratedWhenRegionOrColourIsMissing()
    {
        var detector = new HuntSignalVisionDetector();
        var frame = CreateFrame();

        var result = detector.Detect(frame, new VisionProfile());

        Assert.False(result.IsDetected);
        Assert.Contains("calibrated", result.Status);
    }

    [Fact]
    public void DetectsSampledColourInsideTheCalibratedRegion()
    {
        var detector = new HuntSignalVisionDetector();
        var pixels = Enumerable.Repeat((byte)255, 80 * 60 * 4).ToArray();

        // Solid red square, fully inside the selected region.
        for (var y = 20; y < 34; y++)
        for (var x = 28; x < 42; x++)
        {
            var offset = (y * 80 + x) * 4;
            pixels[offset] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 255;
            pixels[offset + 3] = 255;
        }

        var frame = new ScreenCaptureFrame(
            80, 60, 80 * 4, pixels, DateTimeOffset.UtcNow, "synthetic frame", 1);
        var profile = new VisionProfile
        {
            HuntSignalRegion = new NormalizedRegion(0.20, 0.20, 0.60, 0.60),
            HuntSignalRegionCalibrated = true,
            HuntSignalColor = new HsvColorRange(0, 200, 200, 5, 255, 255),
            MinimumBlobArea = 5,
            MinimumConfidence = 0.60
        };

        var result = detector.Detect(frame, profile);

        Assert.True(result.IsDetected);
        Assert.True(result.Confidence >= 0.60);
        Assert.Contains("detected", result.Status);
    }

    private static ScreenCaptureFrame CreateFrame() =>
        new(80, 60, 80 * 4, Enumerable.Repeat((byte)255, 80 * 60 * 4).ToArray(),
            DateTimeOffset.UtcNow, "synthetic frame", 1);
}
