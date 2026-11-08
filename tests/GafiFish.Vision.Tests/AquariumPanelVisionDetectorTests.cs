using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class AquariumPanelVisionDetectorTests
{
    [Fact]
    public void DetectsCalibratedPanelAnchorWhenMostPixelsMatch()
    {
        var profile = CreateProfile();
        var result = new AquariumPanelVisionDetector().Detect(CreateFrame(showAnchor: true), profile);

        Assert.True(result.IsVisible);
        Assert.Equal(1, result.MatchRatio, precision: 3);
        Assert.Contains("confirmed", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsFrameWhenPanelAnchorIsAbsent()
    {
        var profile = CreateProfile();
        var result = new AquariumPanelVisionDetector().Detect(CreateFrame(showAnchor: false), profile);

        Assert.False(result.IsVisible);
        Assert.Equal(0d, result.MatchRatio);
        Assert.Contains("No action", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UncalibratedPanelAnchorNeverCountsAsVisible()
    {
        var result = new AquariumPanelVisionDetector().Detect(
            CreateFrame(showAnchor: true), new VisionProfile());

        Assert.False(result.IsVisible);
        Assert.Contains("not calibrated", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsInvalidMatchThreshold()
    {
        var profile = CreateProfile() with
        {
            AquariumPanel = CreateProfile().AquariumPanel with { MinimumMatchRatio = 1.1 }
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AquariumPanelVisionDetector().Detect(CreateFrame(showAnchor: true), profile));
    }

    private static VisionProfile CreateProfile() => new()
    {
        AquariumPanel = new AquariumPanelCalibration
        {
            Region = new NormalizedRegion(0.05, 0.05, 0.20, 0.20),
            RegionCalibrated = true,
            PanelColor = HsvColorSampler.FromBgra(0, 255, 0),
            MinimumMatchRatio = 0.60
        }
    };

    private static ScreenCaptureFrame CreateFrame(bool showAnchor)
    {
        const int width = 100;
        const int height = 80;
        var pixels = new byte[width * height * 4];
        if (showAnchor)
        {
            for (var y = 4; y < 20; y++)
            for (var x = 5; x < 25; x++)
            {
                var index = (y * width + x) * 4;
                pixels[index + 1] = 255;
                pixels[index + 3] = 255;
            }
        }

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels,
            new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero),
            "Roblox", 1234);
    }
}
