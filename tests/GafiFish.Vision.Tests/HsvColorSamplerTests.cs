using GafiFish.Vision.Calibration;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class HsvColorSamplerTests
{
    [Fact]
    public void FromBgra_ProducesGreenRangeAroundSampledPixel()
    {
        var range = HsvColorSampler.FromBgra(0, 255, 0);

        Assert.InRange(range.HueMinimum, 22, 30);
        Assert.InRange(range.HueMaximum, 30, 38);
        Assert.Equal(185, range.SaturationMinimum);
        Assert.Equal(255, range.SaturationMaximum);
        Assert.Equal(185, range.ValueMinimum);
        Assert.Equal(255, range.ValueMaximum);
    }

    [Fact]
    public void FromBgra_WhiteSampleAllowsLowSaturationPixels()
    {
        var range = HsvColorSampler.FromBgra(255, 255, 255);

        Assert.Equal(0, range.SaturationMinimum);
        Assert.Equal(70, range.SaturationMaximum);
        Assert.Equal(0, range.HueMinimum);
        Assert.Equal(8, range.HueMaximum);
        Assert.Equal(185, range.ValueMinimum);
        Assert.Equal(255, range.ValueMaximum);
    }

    [Fact]
    public void FromBgra_RejectsExcessiveTolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HsvColorSampler.FromBgra(0, 255, 0, hueTolerance: 60));
    }
}
