using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class ShakePromptVisionDetectorTests
{
    [Fact]
    public void Detect_FindsPromptCandidatesInsideCalibratedRegion()
    {
        var frame = CreateFrame();
        var profile = new VisionProfile
        {
            ShakeRegion = new NormalizedRegion(0.10, 0.10, 0.80, 0.80),
            ShakeRegionCalibrated = true,
            ShakePromptColor = HsvColorSampler.FromBgra(255, 255, 255),
            MinimumBlobArea = 5
        };

        var result = new ShakePromptVisionDetector().Detect(frame, profile);

        Assert.True(result.IsCalibrated);
        Assert.Single(result.Prompts);
        Assert.InRange(result.Prompts[0].CenterX, 37, 43);
        Assert.InRange(result.Prompts[0].CenterY, 27, 33);
    }

    [Fact]
    public void Detect_RequiresExplicitShakeRegionSelection()
    {
        var profile = new VisionProfile
        {
            ShakePromptColor = HsvColorSampler.FromBgra(255, 255, 255)
        };

        var result = new ShakePromptVisionDetector().Detect(CreateFrame(), profile);

        Assert.False(result.IsCalibrated);
        Assert.Empty(result.Prompts);
    }

    [Fact]
    public void Detect_RequiresPromptColorCalibration()
    {
        var result = new ShakePromptVisionDetector().Detect(CreateFrame(), new VisionProfile());

        Assert.False(result.IsCalibrated);
        Assert.Empty(result.Prompts);
        Assert.Contains("Calibrate", result.Status);
    }

    private static ScreenCaptureFrame CreateFrame()
    {
        const int width = 100;
        const int height = 80;
        var pixels = new byte[width * height * 4];

        for (var y = 24; y < 36; y++)
        {
            for (var x = 34; x < 46; x++)
            {
                var index = (y * width + x) * 4;
                pixels[index] = 255;
                pixels[index + 1] = 255;
                pixels[index + 2] = 255;
                pixels[index + 3] = 255;
            }
        }

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels,
            DateTimeOffset.UtcNow, "Roblox", 1234);
    }
}
