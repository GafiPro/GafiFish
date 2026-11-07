using Xunit;
using GafiFish.App.Automation;
using GafiFish.Vision.Detection;

namespace GafiFish.App.Tests;

public sealed class HuntDetectionCoordinatorTests
{
    [Fact]
    public void EmitsOneAlertAfterThreeFreshConfidentFrames()
    {
        var coordinator = new HuntDetectionCoordinator();
        var start = DateTimeOffset.UtcNow;

        Assert.False(coordinator.ProcessObservation(Detected(start)));
        Assert.False(coordinator.ProcessObservation(Detected(start.AddMilliseconds(100))));
        Assert.True(coordinator.ProcessObservation(Detected(start.AddMilliseconds(200))));
        Assert.True(coordinator.HuntIsActive);
        Assert.False(coordinator.ProcessObservation(Detected(start.AddMilliseconds(300))));
    }

    [Fact]
    public void RequiresThreeClearFramesBeforeAnotherAlert()
    {
        var coordinator = new HuntDetectionCoordinator();
        var start = DateTimeOffset.UtcNow;

        Assert.False(coordinator.ProcessObservation(Detected(start)));
        Assert.False(coordinator.ProcessObservation(Detected(start.AddMilliseconds(100))));
        Assert.True(coordinator.ProcessObservation(Detected(start.AddMilliseconds(200))));

        Assert.False(coordinator.ProcessObservation(Clear(start.AddMilliseconds(300))));
        Assert.False(coordinator.ProcessObservation(Clear(start.AddMilliseconds(400))));
        Assert.False(coordinator.ProcessObservation(Clear(start.AddMilliseconds(500))));
        Assert.False(coordinator.HuntIsActive);
        Assert.False(coordinator.ProcessObservation(Detected(start.AddMilliseconds(600))));
        Assert.False(coordinator.ProcessObservation(Detected(start.AddMilliseconds(700))));
        Assert.True(coordinator.ProcessObservation(Detected(start.AddMilliseconds(800))));
    }

    [Fact]
    public void IgnoresDuplicateAndOutOfOrderFrames()
    {
        var coordinator = new HuntDetectionCoordinator();
        var timestamp = DateTimeOffset.UtcNow;

        Assert.False(coordinator.ProcessObservation(Detected(timestamp)));
        Assert.False(coordinator.ProcessObservation(Detected(timestamp)));
        Assert.Contains("duplicate or out-of-order", coordinator.LastStatus);
        Assert.False(coordinator.ProcessObservation(Detected(timestamp.AddMilliseconds(-1))));
        Assert.Contains("duplicate or out-of-order", coordinator.LastStatus);
    }

    [Fact]
    public void LowConfidenceCueDoesNotTriggerAlert()
    {
        var coordinator = new HuntDetectionCoordinator();
        var start = DateTimeOffset.UtcNow;

        for (var index = 0; index < 5; index++)
            Assert.False(coordinator.ProcessObservation(new HuntSignalObservation(
                true, 0.50, "low confidence", start.AddMilliseconds(index * 100))));
        Assert.False(coordinator.HuntIsActive);
    }

    private static HuntSignalObservation Detected(DateTimeOffset at) =>
        new(true, 0.95, "detected", at);

    private static HuntSignalObservation Clear(DateTimeOffset at) =>
        new(false, 0, "clear", at);
}
