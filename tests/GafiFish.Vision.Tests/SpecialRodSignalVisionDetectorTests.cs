using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class SpecialRodSignalVisionDetectorTests
{
    [Fact]
    public void MiguRodDetector_RequiresUserCalibratedRegionAndColour()
    {
        var detector = new SpecialRodSignalVisionDetector();
        var result = detector.DetectMiguRodHazards(CreateBlankFrame(), new VisionProfile());
        Assert.False(result.IsCalibrated);
        Assert.Empty(result.Markers);
        Assert.Contains("sampled colour", result.Status);
    }

    [Fact]
    public void MiguRodDetector_FindsSampledMarkerInsideSelectedRegion()
    {
        var detector = new SpecialRodSignalVisionDetector();
        var profile = new VisionProfile
        {
            MiguRodHazardRegion = new NormalizedRegion(0.20, 0.20, 0.60, 0.60),
            MiguRodHazardRegionCalibrated = true,
            MiguRodHazardColor = new HsvColorRange(0, 200, 200, 5, 255, 255),
            MinimumBlobArea = 5,
            MinimumConfidence = 0.60
        };
        var result = detector.DetectMiguRodHazards(CreateFrameWithRedBlocks(), profile);
        Assert.True(result.IsCalibrated);
        Assert.Single(result.Markers);
        Assert.True(result.Markers[0].Confidence >= 0.60);
        Assert.InRange(result.Markers[0].CenterX, 28, 42);
    }

    [Fact]
    public void DredDetector_DoesNotDetectMatchingColourOutsideSelectedRegion()
    {
        var detector = new SpecialRodSignalVisionDetector();
        var profile = new VisionProfile
        {
            DredBonusObjectRegion = new NormalizedRegion(0.65, 0.65, 0.30, 0.30),
            DredBonusObjectRegionCalibrated = true,
            DredBonusObjectColor = new HsvColorRange(0, 200, 200, 5, 255, 255),
            MinimumBlobArea = 5,
            MinimumConfidence = 0.60
        };
        var result = detector.DetectDredBonusObjects(CreateFrameWithRedBlocks(), profile);
        Assert.True(result.IsCalibrated);
        Assert.Empty(result.Markers);
        Assert.Contains("No calibrated", result.Status);
    }

    [Fact]
    public void DredDetector_RejectsMalformedFrame()
    {
        var detector = new SpecialRodSignalVisionDetector();
        var frame = new ScreenCaptureFrame(80, 60, 80 * 4, [], DateTimeOffset.UtcNow, "bad", 1);
        var profile = new VisionProfile
        {
            DredBonusObjectRegionCalibrated = true,
            DredBonusObjectColor = new HsvColorRange(0, 200, 200, 5, 255, 255)
        };
        Assert.Throws<ArgumentException>(() => detector.DetectDredBonusObjects(frame, profile));
    }

    private static ScreenCaptureFrame CreateBlankFrame() =>
        new(80, 60, 80 * 4, Enumerable.Repeat((byte)255, 80 * 60 * 4).ToArray(),
            DateTimeOffset.UtcNow, "synthetic", 1);

    private static ScreenCaptureFrame CreateFrameWithRedBlocks()
    {
        var pixels = Enumerable.Repeat((byte)255, 80 * 60 * 4).ToArray();
        DrawRedRectangle(pixels, 80, 28, 22, 42, 36);
        DrawRedRectangle(pixels, 80, 2, 2, 14, 14);
        return new ScreenCaptureFrame(80, 60, 80 * 4, pixels, DateTimeOffset.UtcNow, "synthetic", 1);
    }

    private static void DrawRedRectangle(byte[] pixels, int width, int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var offset = (y * width + x) * 4;
            pixels[offset] = 0;
            pixels[offset + 1] = 0;
            pixels[offset + 2] = 255;
            pixels[offset + 3] = 255;
        }
    }
}