using GafiFish.Core.Automation;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class CaptureCadencePolicyTests
{
    [Theory]
    [InlineData(CapturePerformanceMode.Economy, 260)]
    [InlineData(CapturePerformanceMode.Balanced, 120)]
    [InlineData(CapturePerformanceMode.Responsive, 55)]
    public void GetTargetIntervalMilliseconds_ReturnsPresetTarget(CapturePerformanceMode mode, int expected)
    {
        Assert.Equal(expected, CaptureCadencePolicy.GetTargetIntervalMilliseconds(mode));
    }

    [Theory]
    [InlineData(CapturePerformanceMode.Economy, 20, 240)]
    [InlineData(CapturePerformanceMode.Balanced, 20, 100)]
    [InlineData(CapturePerformanceMode.Responsive, 20, 35)]
    public void CalculateDelayMilliseconds_AdaptsToProcessingTime(CapturePerformanceMode mode, double processing, int expected)
    {
        Assert.Equal(expected, CaptureCadencePolicy.CalculateDelayMilliseconds(mode, processing));
    }

    [Theory]
    [InlineData(CapturePerformanceMode.Economy, 999, 15)]
    [InlineData(CapturePerformanceMode.Balanced, 999, 15)]
    [InlineData(CapturePerformanceMode.Responsive, 999, 8)]
    public void CalculateDelayMilliseconds_RespectsMinimumDelay(CapturePerformanceMode mode, double processing, int expected)
    {
        Assert.Equal(expected, CaptureCadencePolicy.CalculateDelayMilliseconds(mode, processing));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void CalculateDelayMilliseconds_RejectsInvalidProcessingTimes(double processing)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CaptureCadencePolicy.CalculateDelayMilliseconds(CapturePerformanceMode.Balanced, processing));
    }
}
