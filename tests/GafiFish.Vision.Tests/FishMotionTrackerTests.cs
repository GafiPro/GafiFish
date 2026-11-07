using GafiFish.Vision.Detection;
using GafiFish.Vision.Tracking;
using OpenCvSharp;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class FishMotionTrackerTests
{
    [Fact]
    public void Update_EstimatesHorizontalVelocityAndShortPrediction()
    {
        var tracker = new FishMotionTracker(smoothing: 1);
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        tracker.Update(Marker(100), start);
        var estimate = tracker.Update(Marker(120), start.AddMilliseconds(100));

        Assert.NotNull(estimate);
        Assert.Equal(200, estimate.VelocityPixelsPerSecond, precision: 5);
        Assert.Equal(138, estimate.PredictedPositionX, precision: 5);
        Assert.True(estimate.IsFresh);
    }

    [Fact]
    public void GetEstimate_ExpiresWhenDetectionsStop()
    {
        var tracker = new FishMotionTracker();
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        tracker.Update(Marker(100), start);

        var estimate = tracker.GetEstimate(start.AddSeconds(1));

        Assert.Null(estimate);
    }

    [Fact]
    public void MissingDetectionOnlyReturnsBriefDecayingEstimate()
    {
        var tracker = new FishMotionTracker();
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        tracker.Update(Marker(100), start);

        var estimate = tracker.Update(null, start.AddMilliseconds(90));

        Assert.NotNull(estimate);
        Assert.False(estimate.IsFresh);
        Assert.True(estimate.Confidence < 0.75);
    }

    [Fact]
    public void Update_IgnoresDuplicateAndOutOfOrderFramesWithoutRollingBackState()
    {
        var tracker = new FishMotionTracker(smoothing: 1);
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        tracker.Update(Marker(100), start);
        var latest = tracker.Update(Marker(120), start.AddMilliseconds(100));
        Assert.NotNull(latest);
        Assert.Equal(200, latest.VelocityPixelsPerSecond, precision: 5);

        Assert.Null(tracker.Update(Marker(900), start.AddMilliseconds(100)));
        Assert.Null(tracker.Update(Marker(800), start.AddMilliseconds(50)));

        var next = tracker.Update(Marker(140), start.AddMilliseconds(200));
        Assert.NotNull(next);
        Assert.Equal(200, next.VelocityPixelsPerSecond, precision: 5);
        Assert.Equal(140, next.PositionX, precision: 5);
    }

    [Fact]
    public void Update_IgnoresNonFiniteConfidenceWithoutPoisoningVelocity()
    {
        var tracker = new FishMotionTracker(smoothing: 1);
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        tracker.Update(Marker(100), start);
        var invalid = Marker(900) with { Confidence = double.NaN };
        var decayed = tracker.Update(invalid, start.AddMilliseconds(100));

        Assert.NotNull(decayed);
        Assert.False(decayed.IsFresh);
        Assert.Equal(100, decayed.PositionX, precision: 5);

        var next = tracker.Update(Marker(120), start.AddMilliseconds(200));
        Assert.NotNull(next);
        Assert.Equal(100, next.VelocityPixelsPerSecond, precision: 5);
    }

    [Fact]
    public void GetEstimate_EarlierTimestampDoesNotResetLatestTrack()
    {
        var tracker = new FishMotionTracker(smoothing: 1);
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        tracker.Update(Marker(100), start);
        tracker.Update(Marker(120), start.AddMilliseconds(100));

        Assert.Null(tracker.GetEstimate(start.AddMilliseconds(50)));

        var next = tracker.Update(Marker(140), start.AddMilliseconds(200));
        Assert.NotNull(next);
        Assert.Equal(200, next.VelocityPixelsPerSecond, precision: 5);
    }

    private static DetectedMarker Marker(double x) =>
        new(x, 40, new Rect((int)x - 3, 37, 6, 6), 25, 0.95);
}
