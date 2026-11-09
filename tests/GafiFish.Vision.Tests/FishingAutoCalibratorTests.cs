using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class FishingAutoCalibratorTests
{
    [Fact]
    public void Create_SamplesBothMarkersAndSuggestsSearchRegion()
    {
        const int width = 320;
        const int height = 180;
        var frame = CreateFrame(width, height);
        Fill(frame, width, 78, 88, 84, 94, blue: 20, green: 35, red: 230);
        Fill(frame, width, 214, 86, 226, 98, blue: 230, green: 45, red: 25);

        var suggestion = FishingAutoCalibrator.Create(frame, 81, 91, 220, 92);
        var pixels = suggestion.SearchRegion.ToPixelRect(width, height);

        Assert.True(suggestion.FishMarkerColor.HueMinimum <= suggestion.FishMarkerColor.HueMaximum);
        Assert.NotEqual(suggestion.FishMarkerColor, suggestion.ControlMarkerColor);
        Assert.True(pixels.Contains(81, 91));
        Assert.True(pixels.Contains(220, 92));
        Assert.True(pixels.Width >= width * 0.34);
        Assert.True(pixels.Height >= height * 0.14);
    }

    [Fact]
    public void Create_RejectsSamplePointsThatAreTooClose()
    {
        const int width = 160;
        const int height = 100;
        var frame = CreateFrame(width, height);
        Fill(frame, width, 20, 20, 40, 40, blue: 30, green: 40, red: 220);

        Assert.Throws<InvalidOperationException>(
            () => FishingAutoCalibrator.Create(frame, 25, 25, 27, 26));
    }

    [Fact]
    public void Create_RejectsMarkersWithNearlyIdenticalColours()
    {
        const int width = 160;
        const int height = 100;
        var frame = CreateFrame(width, height);
        Fill(frame, width, 20, 20, 30, 30, blue: 30, green: 50, red: 200);
        Fill(frame, width, 110, 20, 120, 30, blue: 33, green: 51, red: 202);

        Assert.Throws<InvalidOperationException>(
            () => FishingAutoCalibrator.Create(frame, 25, 25, 115, 25));
    }

    [Fact]
    public void Create_RejectsInvalidFrameBuffer()
    {
        var frame = new ScreenCaptureFrame(
            100, 80, 400, new byte[20], DateTimeOffset.UtcNow, "Roblox", 1234);

        Assert.Throws<ArgumentException>(
            () => FishingAutoCalibrator.Create(frame, 20, 20, 70, 20));
    }

    private static ScreenCaptureFrame CreateFrame(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var offset = (y * width + x) * 4;
            pixels[offset] = 14;
            pixels[offset + 1] = 18;
            pixels[offset + 2] = 24;
            pixels[offset + 3] = 255;
        }

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels, DateTimeOffset.UtcNow, "Roblox", 1234);
    }

    private static void Fill(
        ScreenCaptureFrame frame,
        int frameWidth,
        int left,
        int top,
        int right,
        int bottom,
        byte blue,
        byte green,
        byte red)
    {
        for (var y = top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
        {
            var offset = (y * frameWidth + x) * 4;
            frame.BgraPixels[offset] = blue;
            frame.BgraPixels[offset + 1] = green;
            frame.BgraPixels[offset + 2] = red;
            frame.BgraPixels[offset + 3] = 255;
        }
    }
}
