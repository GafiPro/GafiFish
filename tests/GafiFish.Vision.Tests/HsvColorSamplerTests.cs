using GafiFish.Vision.Calibration;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class HsvColorSamplerTests
{
    [Fact]
    public void FromBgra_ProducesGreenRangeAroundSampledPixel()
    {
        var range = HsvColorSampler.FromBgra(0, 255, 0);

        Assert.InRange(range.HueMinimum, 52, 60);
        Assert.InRange(range.HueMaximum, 60, 68);
        Assert.Equal(185, range.SaturationMinimum);
        Assert.Equal(255, range.SaturationMaximum);
        Assert.Equal(185, range.ValueMinimum);
        Assert.Equal(255, range.ValueMaximum);
    }

    [Fact]
    public void FromBgra_WhiteSampleAllowsLowSaturationPixelsAcrossAllHues()
    {
        var range = HsvColorSampler.FromBgra(255, 255, 255);

        Assert.Equal(0, range.HueMinimum);
        Assert.Equal(179, range.HueMaximum);
        Assert.Equal(0, range.SaturationMinimum);
        Assert.Equal(70, range.SaturationMaximum);
        Assert.Equal(185, range.ValueMinimum);
        Assert.Equal(255, range.ValueMaximum);
    }

    [Fact]
    public void FromBgra_RedSampleWrapsHueAcrossZero()
    {
        var range = HsvColorSampler.FromBgra(0, 0, 255);

        Assert.Equal(172, range.HueMinimum);
        Assert.Equal(8, range.HueMaximum);
        range.Validate();
    }

    [Fact]
    public void FromBgra_RejectsExcessiveTolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HsvColorSampler.FromBgra(0, 255, 0, hueTolerance: 60));
    }
}
