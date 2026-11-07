using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class ConnectionRecoveryVisionDetectorTests
{
    [Fact]
    public void DetectsOnlyInsideUserCalibratedReconnectRegion()
    {
        var detected = new ConnectionRecoveryVisionDetector()
            .DetectReconnectButton(CreateFrame(true), CreateProfile());

        Assert.NotNull(detected);
        Assert.InRange(detected.CenterX, 60, 80);
        Assert.InRange(detected.CenterY, 20, 40);
        Assert.True(detected.Confidence >= 0.65);
    }

    [Fact]
    public void DoesNotGuessWithoutCalibration()
    {
        var detected = new ConnectionRecoveryVisionDetector()
            .DetectReconnectButton(CreateFrame(true), new VisionProfile());

        Assert.Null(detected);
    }

    [Fact]
    public void IgnoresMatchingColourOutsideTheCalibratedRegion()
    {
        var profile = CreateProfile() with
        {
            ReconnectButtonRegion = new NormalizedRegion(0.05, 0.05, 0.25, 0.30)
        };

        var detected = new ConnectionRecoveryVisionDetector()
            .DetectReconnectButton(CreateFrame(true), profile);

        Assert.Null(detected);
    }

    private static VisionProfile CreateProfile() => new()
    {
        MinimumBlobArea = 5,
        ReconnectButtonRegion = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
        ReconnectButtonRegionCalibrated = true,
        ReconnectButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
    };

    private static ScreenCaptureFrame CreateFrame(bool showButton)
    {
        const int width = 100;
        const int height = 80;
        var pixels = new byte[width * height * 4];
        if (showButton)
        {
            for (var y = 20; y < 40; y++)
            for (var x = 60; x < 80; x++)
            {
                var index = (y * width + x) * 4;
                pixels[index + 2] = 255;
                pixels[index + 3] = 255;
            }
        }

        return new ScreenCaptureFrame(width, height, width * 4, pixels,
            DateTimeOffset.UtcNow, "Roblox", 1234);
    }
}
