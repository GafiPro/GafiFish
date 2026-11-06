using GafiFish.Core.Aquarium;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class AquariumButtonVisionDetectorTests
{
    [Fact]
    public void DetectsReadyButtonInsideItsCalibratedRegion()
    {
        var profile = CreateProfile();
        var result = new AquariumButtonVisionDetector().Detect(
            CreateFrame(showButton: true), profile, AquariumActionKind.FeedGuppiesBest);

        Assert.NotNull(result);
        Assert.InRange(result.CenterX, 60, 80);
        Assert.InRange(result.CenterY, 20, 40);
        Assert.True(result.Confidence >= 0.65);
    }

    [Fact]
    public void DoesNotGuessWhenButtonIsNotCalibrated()
    {
        var result = new AquariumButtonVisionDetector().Detect(
            CreateFrame(showButton: true), new VisionProfile(), AquariumActionKind.FeedGuppiesBest);

        Assert.Null(result);
    }

    [Fact]
    public void DoesNotDetectButtonOutsideItsCalibratedRegion()
    {
        var profile = CreateProfile() with
        {
            AquariumButtons = new Dictionary<AquariumActionKind, AquariumButtonCalibration>
            {
                [AquariumActionKind.FeedGuppiesBest] = new()
                {
                    Region = new NormalizedRegion(0.05, 0.05, 0.25, 0.30),
                    RegionCalibrated = true,
                    ButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
                }
            }
        };

        var result = new AquariumButtonVisionDetector().Detect(
            CreateFrame(showButton: true), profile, AquariumActionKind.FeedGuppiesBest);

        Assert.Null(result);
    }

    private static VisionProfile CreateProfile() => new()
    {
        MinimumBlobArea = 5,
        AquariumButtons = new Dictionary<AquariumActionKind, AquariumButtonCalibration>
        {
            [AquariumActionKind.FeedGuppiesBest] = new()
            {
                Region = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
                RegionCalibrated = true,
                ButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
            }
        }
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
                pixels[index] = 0;
                pixels[index + 1] = 0;
                pixels[index + 2] = 255;
                pixels[index + 3] = 255;
            }
        }

        return new ScreenCaptureFrame(width, height, width * 4, pixels,
            DateTimeOffset.UtcNow, "Roblox", 1234);
    }
}
