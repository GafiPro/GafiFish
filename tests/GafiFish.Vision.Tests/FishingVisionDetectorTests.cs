using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class FishingVisionDetectorTests
{
    [Fact]
    public void Detect_DoesNotGuessMarkerColorsBeforeCalibration()
    {
        var frame = new ScreenCaptureFrame(
            4, 4, 16, new byte[64],
            new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero),
            "Roblox", 1234);

        var result = new FishingVisionDetector().Detect(frame, new VisionProfile());

        Assert.False(result.IsCalibrated);
        Assert.Null(result.FishMarker);
        Assert.Null(result.ControlMarker);
        Assert.Contains("Calibrate", result.Status);
    }

    [Fact]
    public void Detect_RejectsInvalidPixelBuffer()
    {
        var frame = new ScreenCaptureFrame(
            4, 4, 16, new byte[8],
            DateTimeOffset.UtcNow, "Roblox", 1234);

        Assert.Throws<ArgumentException>(
            () => new FishingVisionDetector().Detect(frame, new VisionProfile()));
    }
}
