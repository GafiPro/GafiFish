using System.Runtime.InteropServices;
using GafiFish.Vision.Calibration;
using OpenCvSharp;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class HsvColorRangeTests
{
    [Fact]
    public void ApplyMask_IncludesBothSidesOfWrappedHueAndExcludesOtherColours()
    {
        using var hsv = new Mat(1, 3, MatType.CV_8UC3);
        byte[] pixels = [178, 255, 255, 2, 255, 255, 90, 255, 255];
        Marshal.Copy(pixels, 0, hsv.Data, pixels.Length);
        using var mask = new Mat();

        var red = new HsvColorRange(172, 200, 200, 8, 255, 255);
        red.ApplyMask(hsv, mask);

        Assert.Equal(255, mask.At<byte>(0, 0));
        Assert.Equal(255, mask.At<byte>(0, 1));
        Assert.Equal(0, mask.At<byte>(0, 2));
    }

    [Fact]
    public void ApplyMask_StillSupportsNormalNonWrappedIntervals()
    {
        using var hsv = new Mat(1, 2, MatType.CV_8UC3);
        byte[] pixels = [60, 255, 255, 110, 255, 255];
        Marshal.Copy(pixels, 0, hsv.Data, pixels.Length);
        using var mask = new Mat();

        var green = new HsvColorRange(52, 180, 180, 68, 255, 255);
        green.ApplyMask(hsv, mask);

        Assert.Equal(255, mask.At<byte>(0, 0));
        Assert.Equal(0, mask.At<byte>(0, 1));
    }

    [Fact]
    public void Validate_RejectsInvalidSaturationAndValueBoundsEvenWhenHueWraps()
    {
        var invalidSaturation = new HsvColorRange(172, 240, 200, 8, 100, 255);
        var invalidValue = new HsvColorRange(172, 100, 250, 8, 255, 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => invalidSaturation.Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => invalidValue.Validate());
    }
}
