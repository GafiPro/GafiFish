using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class RodChargeMeterVisionDetectorTests
{
    [Fact]
    public void Detect_ReturnsHorizontalFillRatioFromCalibratedRegion()
    {
        var frame = CreateFrame(fillWidth: 65, fromRight: false);
        var profile = CreateProfile(fillFromRight: false);

        var result = new RodChargeMeterVisionDetector().Detect(frame, profile);

        Assert.NotNull(result);
        Assert.True(result.IsUsable);
        Assert.InRange(result.Progress, 0.80, 0.83);
        Assert.True(result.Confidence >= 0.9);
    }

    [Fact]
    public void Detect_SupportsFillThatStartsOnTheRight()
    {
        var frame = CreateFrame(fillWidth: 70, fromRight: true);
        var profile = CreateProfile(fillFromRight: true);

        var result = new RodChargeMeterVisionDetector().Detect(frame, profile);

        Assert.NotNull(result);
        Assert.True(result.IsUsable);
        Assert.InRange(result.Progress, 0.86, 0.89);
    }

    [Fact]
    public void Detect_ReturnsNullWhenCalibrationIsMissing()
    {
        var result = new RodChargeMeterVisionDetector().Detect(
            CreateFrame(fillWidth: 65, fromRight: false),
            new VisionProfile());

        Assert.Null(result);
    }

    [Fact]
    public void Detect_ReturnsUnusableObservationWhenFillColourIsNotVisible()
    {
        var result = new RodChargeMeterVisionDetector().Detect(
            CreateFrame(fillWidth: 0, fromRight: false),
            CreateProfile(fillFromRight: false));

        Assert.NotNull(result);
        Assert.False(result.IsUsable);
    }

    [Fact]
    public void VisionProfile_RequiresChargeCalibrationOnlyForSelectedMode()
    {
        var standard = new VisionProfile
        {
            RodMinigameMode = GafiFish.Core.Control.RodMinigameMode.StandardReel
        };
        var charge = new VisionProfile
        {
            RodMinigameMode = GafiFish.Core.Control.RodMinigameMode.ChargeMeterBonus
        };

        standard.Validate();
        charge.Validate();
        Assert.False(standard.IsRodChargeMeterCalibrated);
        Assert.False(charge.IsRodChargeMeterCalibrated);
    }

    private static VisionProfile CreateProfile(bool fillFromRight) => new()
    {
        RodMinigameMode = GafiFish.Core.Control.RodMinigameMode.ChargeMeterBonus,
        RodChargeMeterRegion = new NormalizedRegion(0.1, 0.25, 0.8, 0.3),
        RodChargeMeterRegionCalibrated = true,
        RodChargeMeterColor = HsvColorSampler.FromBgra(0, 255, 0),
        RodChargeMeterFillFromRight = fillFromRight
    };

    private static ScreenCaptureFrame CreateFrame(int fillWidth, bool fromRight)
    {
        const int width = 100;
        const int height = 50;
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = 255;

        var left = fromRight ? 90 - fillWidth : 10;
        for (var y = 12; y < 28; y++)
        for (var x = left; x < left + fillWidth; x++)
        {
            var index = (y * width + x) * 4;
            pixels[index] = 0;
            pixels[index + 1] = 255;
            pixels[index + 2] = 0;
        }

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels,
            DateTimeOffset.UtcNow, "Roblox", 1234);
    }
}
