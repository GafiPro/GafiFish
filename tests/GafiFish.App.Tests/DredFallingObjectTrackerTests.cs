using GafiFish.App.Automation;
using GafiFish.Vision.Detection;
using OpenCvSharp;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class DredFallingObjectTrackerTests
{
    [Fact]
    public void RequiresConsistentDownwardMovementAcrossDistinctFrames()
    {
        var tracker = new DredFallingObjectTracker();
        var start = DateTimeOffset.UtcNow;

        Assert.Empty(tracker.ProcessObservation(start, [Marker(100, 100)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(100), [Marker(101, 103)]));
        var falling = tracker.ProcessObservation(start.AddMilliseconds(200), [Marker(102, 106)]);

        Assert.Single(falling);
        Assert.Equal(102, falling[0].CenterX);
        Assert.Equal(106, falling[0].CenterY);
    }

    [Fact]
    public void AStationaryOrHorizontallyMovingCandidateDoesNotCountAsFalling()
    {
        var tracker = new DredFallingObjectTracker();
        var start = DateTimeOffset.UtcNow;

        Assert.Empty(tracker.ProcessObservation(start, [Marker(20, 20)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(100), [Marker(30, 20)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(200), [Marker(40, 20)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(300), [Marker(50, 20)]));
    }

    [Fact]
    public void UpwardMovementResetsPreviouslyAccumulatedDownwardEvidence()
    {
        var tracker = new DredFallingObjectTracker();
        var start = DateTimeOffset.UtcNow;

        Assert.Empty(tracker.ProcessObservation(start, [Marker(100, 100)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(100), [Marker(100, 104)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(200), [Marker(100, 101)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(300), [Marker(100, 105)]));
        Assert.Single(tracker.ProcessObservation(start.AddMilliseconds(400), [Marker(100, 109)]));
        Assert.Single(tracker.ProcessObservation(start.AddMilliseconds(500), [Marker(100, 113)]));
    }

    [Fact]
    public void DuplicateAndOutOfOrderFramesDoNotAdvanceTrajectory()
    {
        var tracker = new DredFallingObjectTracker();
        var start = DateTimeOffset.UtcNow;

        Assert.Empty(tracker.ProcessObservation(start, [Marker(100, 100)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(100), [Marker(100, 103)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(100), [Marker(100, 106)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(90), [Marker(100, 109)]));
        Assert.Single(tracker.ProcessObservation(start.AddMilliseconds(200), [Marker(100, 106)]));
    }

    [Fact]
    public void ExpiredTracksAreNotMatchedAfterLongFrameGap()
    {
        var tracker = new DredFallingObjectTracker(maximumFrameGap: TimeSpan.FromMilliseconds(150));
        var start = DateTimeOffset.UtcNow;

        Assert.Empty(tracker.ProcessObservation(start, [Marker(100, 100)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(200), [Marker(100, 110)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(300), [Marker(100, 120)]));
        Assert.Single(tracker.ProcessObservation(start.AddMilliseconds(400), [Marker(100, 130)]));
    }

    [Fact]
    public void ResetClearsFrameHistoryAndTracks()
    {
        var tracker = new DredFallingObjectTracker();
        var start = DateTimeOffset.UtcNow;

        Assert.Empty(tracker.ProcessObservation(start, [Marker(100, 100)]));
        Assert.Equal(1, tracker.ActiveTrackCount);
        tracker.Reset();

        Assert.Equal(0, tracker.ActiveTrackCount);
        Assert.Empty(tracker.ProcessObservation(start, [Marker(100, 100)]));
        Assert.Empty(tracker.ProcessObservation(start.AddMilliseconds(100), [Marker(100, 103)]));
    }

    [Theory]
    [InlineData(0, 1.5, 5, 64)]
    [InlineData(2, double.NaN, 5, 64)]
    [InlineData(2, 2, 1, 64)]
    [InlineData(2, 1.5, 5, double.PositiveInfinity)]
    public void ConstructorRejectsInvalidSettings(
        int requiredSteps, double step, double total, double matchDistance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DredFallingObjectTracker(requiredSteps, step, total, matchDistance));
    }

    private static DetectedMarker Marker(double x, double y) =>
        new(x, y, new Rect((int)x - 2, (int)y - 2, 4, 4), 16, 0.9);
}
