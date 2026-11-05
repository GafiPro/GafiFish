using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class SellAnywhereVisionDetectorTests
{
    [Fact]
    public void DetectButton_FindsCalibratedButtonColorInRegion()
    {
        var frame = CreateFrame(fillRedButton: true);
        var profile = new VisionProfile
        {
            SellButtonRegion = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
            SellButtonRegionCalibrated = true,
            SellButtonColor = HsvColorSampler.FromBgra(0, 0, 255),
            MinimumBlobArea = 5
        };

        var result = new SellAnywhereVisionDetector().DetectButton(frame, profile);

        Assert.True(result.IsCalibrated);
        Assert.NotNull(result.SellButton);
        Assert.InRange(result.SellButton.CenterX, 65, 67);
    }

    [Fact]
    public void DetectButton_DoesNotGuessColorBeforeCalibration()
    {
        var result = new SellAnywhereVisionDetector().DetectButton(
            CreateFrame(fillRedButton: true), new VisionProfile());

        Assert.False(result.IsCalibrated);
        Assert.Null(result.SellButton);
    }

    [Fact]
    public void MeasureInventoryChange_ReturnsHighRatioWhenListAreaChanges()
    {
        var before = CreateFrame(fillRedButton: false);
        var after = CreateFrame(fillRedButton: false, fillInventoryList: true);

        var ratio = new SellAnywhereVisionDetector().MeasureInventoryChange(
            before, after, new NormalizedRegion(0.10, 0.10, 0.30, 0.80));

        Assert.InRange(ratio, 0.9, 1.0);
    }

    [Fact]
    public void MeasureInventoryChange_ReturnsZeroForIdenticalFrames()
    {
        var frame = CreateFrame(fillRedButton: false);

        var ratio = new SellAnywhereVisionDetector().MeasureInventoryChange(
            frame, frame, new NormalizedRegion(0.10, 0.10, 0.30, 0.80));

        Assert.Equal(0, ratio);
    }

    private static ScreenCaptureFrame CreateFrame(
        bool fillRedButton,
        bool fillInventoryList = false)
    {
        const int width = 100;
        const int height = 80;
        var pixels = new byte[width * height * 4];

        if (fillRedButton)
        {
            FillRectangle(pixels, width, 60, 20, 12, 12, 0, 0, 255);
        }

        if (fillInventoryList)
        {
            FillRectangle(pixels, width, 10, 10, 30, 60, 255, 255, 255);
        }

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels,
            DateTimeOffset.UtcNow, "Roblox", 1234);
    }

    private static void FillRectangle(
        byte[] pixels, int frameWidth, int x, int y, int width, int height,
        byte blue, byte green, byte red)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                var index = (row * frameWidth + column) * 4;
                pixels[index] = blue;
                pixels[index + 1] = green;
                pixels[index + 2] = red;
                pixels[index + 3] = 255;
            }
        }
    }
}
