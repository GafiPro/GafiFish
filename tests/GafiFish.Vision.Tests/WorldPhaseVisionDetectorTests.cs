using GafiFish.Core.Automation;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class WorldPhaseVisionDetectorTests
{
    [Fact]
    public void DetectsDayFromItsCalibratedColour()
    {
        var result = new WorldPhaseVisionDetector().Detect(
            CreateFrame(day: true, night: false), CreateProfile());

        Assert.Equal(DayNightPhase.Day, result.Phase);
        Assert.True(result.Confidence >= 0.65);
    }

    [Fact]
    public void DetectsNightFromItsCalibratedColour()
    {
        var result = new WorldPhaseVisionDetector().Detect(
            CreateFrame(day: false, night: true), CreateProfile());

        Assert.Equal(DayNightPhase.Night, result.Phase);
        Assert.True(result.Confidence >= 0.65);
    }

    [Fact]
    public void ReturnsUnknownWhenBothColoursAreVisible()
    {
        var result = new WorldPhaseVisionDetector().Detect(
            CreateFrame(day: true, night: true), CreateProfile());

        Assert.Equal(DayNightPhase.Unknown, result.Phase);
        Assert.Contains("ambiguous", result.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReturnsUnknownWithoutBothPhaseCalibrations()
    {
        var result = new WorldPhaseVisionDetector().Detect(
            CreateFrame(day: true, night: false),
            new VisionProfile());

        Assert.Equal(DayNightPhase.Unknown, result.Phase);
    }

    private static VisionProfile CreateProfile() => new()
    {
        MinimumBlobArea = 5,
        WorldPhaseRegion = new NormalizedRegion(0.40, 0.15, 0.45, 0.70),
        WorldPhaseRegionCalibrated = true,
        DayPhaseColor = HsvColorSampler.FromBgra(0, 0, 255, 5, 35, 35),
        NightPhaseColor = HsvColorSampler.FromBgra(255, 0, 0, 5, 35, 35)
    };

    private static ScreenCaptureFrame CreateFrame(bool day, bool night)
    {
        const int width = 100;
        const int height = 80;
        var pixels = new byte[width * height * 4];

        if (day)
            FillRect(pixels, width, left: 55, top: 25, rectWidth: 20, rectHeight: 15, blue: 0, green: 0, red: 255);
        if (night)
            FillRect(pixels, width, left: 55, top: 45, rectWidth: 20, rectHeight: 15, blue: 255, green: 0, red: 0);

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels,
            DateTimeOffset.UtcNow, "Roblox", 1234);
    }

    private static void FillRect(
        byte[] pixels,
        int frameWidth,
        int left,
        int top,
        int rectWidth,
        int rectHeight,
        byte blue,
        byte green,
        byte red)
    {
        for (var y = top; y < top + rectHeight; y++)
        for (var x = left; x < left + rectWidth; x++)
        {
            var index = (y * frameWidth + x) * 4;
            pixels[index] = blue;
            pixels[index + 1] = green;
            pixels[index + 2] = red;
            pixels[index + 3] = 255;
        }
    }
}
