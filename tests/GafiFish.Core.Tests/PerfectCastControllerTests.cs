using GafiFish.Core.Casting;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class PerfectCastControllerTests
{
    [Fact]
    public void Evaluate_WaitsBeforeReleaseWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new PerfectCastController();
        var observation = new CastMeterObservation(
            true, 0.30, 0.50, 0.60, 0.10, 0.99, now);

        var decision = controller.Evaluate(observation, now);

        Assert.Equal(PerfectCastDecisionKind.Wait, decision.Kind);
    }

    [Fact]
    public void Evaluate_ReleasesWhenLatencyPredictionEntersTargetZone()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new PerfectCastController(new PerfectCastSettings
        {
            EstimatedInputLatency = TimeSpan.FromMilliseconds(100)
        });
        var observation = new CastMeterObservation(
            true, 0.48, 0.50, 0.60, 0.30, 0.99, now);

        var decision = controller.Evaluate(observation, now);

        Assert.Equal(PerfectCastDecisionKind.Release, decision.Kind);
    }

    [Fact]
    public void Evaluate_CancelsOnStaleFrame()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new PerfectCastController();
        var observation = new CastMeterObservation(
            true, 0.48, 0.50, 0.60, 0.10, 0.99, now.AddSeconds(-1));

        var decision = controller.Evaluate(observation, now);

        Assert.Equal(PerfectCastDecisionKind.Cancel, decision.Kind);
    }


    [Fact]
    public void Evaluate_ReleasesWhenIndicatorApproachesFromTheRight()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new PerfectCastController(new PerfectCastSettings
        {
            EstimatedInputLatency = TimeSpan.FromMilliseconds(100)
        });
        var observation = new CastMeterObservation(
            true, 0.65, 0.50, 0.60, -1.0, 0.99, now);

        var decision = controller.Evaluate(observation, now);

        Assert.Equal(PerfectCastDecisionKind.Release, decision.Kind);
        Assert.InRange(decision.PredictedIndicatorPosition, 0.54, 0.56);
    }

    [Fact]
    public void Evaluate_CancelsWhenTargetHasAlreadyBeenPassed()
    {
        var now = DateTimeOffset.UtcNow;
        var controller = new PerfectCastController();
        var observation = new CastMeterObservation(
            true, 0.72, 0.50, 0.60, 0.10, 0.99, now);

        var decision = controller.Evaluate(observation, now);

        Assert.Equal(PerfectCastDecisionKind.Cancel, decision.Kind);
    }
}
