using GafiFish.App.Automation;
using GafiFish.Vision.Detection;
using OpenCvSharp;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class DualFishCoverageEvaluatorTests
{
    [Fact]
    public void CountCoveredFish_ReturnsTwoWhenBothFishCentresAreInsideTheBar()
    {
        var count = DualFishCoverageEvaluator.CountCoveredFish(
            Marker(40), Marker(60), Bar(30, 40));
        Assert.Equal(2, count);
    }

    [Fact]
    public void CountCoveredFish_ReturnsOneWhenOnlyOneFishCentreIsInsideTheBar()
    {
        var count = DualFishCoverageEvaluator.CountCoveredFish(
            Marker(40), Marker(90), Bar(30, 40));
        Assert.Equal(1, count);
    }

    [Fact]
    public void CountCoveredFish_ReturnsZeroWhenNeitherFishCentreIsInsideTheBar()
    {
        var count = DualFishCoverageEvaluator.CountCoveredFish(
            Marker(10), Marker(90), Bar(30, 40));
        Assert.Equal(0, count);
    }

    [Fact]
    public void CountCoveredFish_ReturnsZeroWhenTheControlBarIsMissing()
    {
        var count = DualFishCoverageEvaluator.CountCoveredFish(
            Marker(40), Marker(60), null);
        Assert.Equal(0, count);
    }

    private static DetectedMarker Marker(double centerX) =>
        new(centerX, 40, new Rect((int)centerX - 3, 37, 6, 6), 25, 0.95);

    private static DetectedMarker Bar(int left, int width) =>
        new(left + width / 2d, 40, new Rect(left, 37, width, 6), width * 6d, 0.95);
}
