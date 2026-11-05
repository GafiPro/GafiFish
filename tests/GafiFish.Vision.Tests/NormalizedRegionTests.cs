using GafiFish.Vision.Calibration;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class NormalizedRegionTests
{
    [Fact]
    public void ToPixelRect_MapsFractionalRegionToFramePixels()
    {
        var region = new NormalizedRegion(0.25, 0.50, 0.50, 0.25);

        var pixels = region.ToPixelRect(1920, 1080);

        Assert.Equal(480, pixels.X);
        Assert.Equal(540, pixels.Y);
        Assert.Equal(960, pixels.Width);
        Assert.Equal(270, pixels.Height);
    }

    [Fact]
    public void ToPixelRect_MapsFullFrameWithoutClipping()
    {
        var pixels = NormalizedRegion.FullFrame.ToPixelRect(1366, 768);

        Assert.Equal(0, pixels.X);
        Assert.Equal(0, pixels.Y);
        Assert.Equal(1366, pixels.Width);
        Assert.Equal(768, pixels.Height);
    }

    [Theory]
    [InlineData(-0.1, 0, 0.5, 0.5)]
    [InlineData(0.8, 0, 0.3, 0.5)]
    [InlineData(0, 0, 0, 0.5)]
    [InlineData(0, double.NaN, 0.5, 0.5)]
    public void Validate_RejectsInvalidRegions(double x, double y, double width, double height)
    {
        var region = new NormalizedRegion(x, y, width, height);

        Assert.Throws<ArgumentOutOfRangeException>(() => region.Validate());
    }
}
