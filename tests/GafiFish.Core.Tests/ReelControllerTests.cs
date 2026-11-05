using GafiFish.Core.Control;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class ReelControllerTests
{
    [Fact]
    public void Evaluate_HoldsWhenPredictedFishPositionIsRightOfControlBar()
    {
        var controller = new ReelController(new ReelControlSettings
        {
            ProportionalGain = 1,
            DerivativeGain = 0,
            Deadband = 5,
            LookAheadSeconds = 0
        });

        var decision = controller.Evaluate(150, 100, 0, 0.95, isFresh: true);

        Assert.Equal(ReelCommand.Hold, decision.Command);
        Assert.Equal(50, decision.PositionError);
    }

    [Fact]
    public void Evaluate_ReleasesWhenPredictedFishPositionIsLeftOfControlBar()
    {
        var controller = new ReelController(new ReelControlSettings
        {
            ProportionalGain = 1,
            DerivativeGain = 0,
            Deadband = 5,
            LookAheadSeconds = 0
        });

        var decision = controller.Evaluate(60, 100, 0, 0.95, isFresh: true);

        Assert.Equal(ReelCommand.Release, decision.Command);
        Assert.Equal(-40, decision.PositionError);
    }

    [Fact]
    public void Evaluate_ReturnsNeutralInsideDeadband()
    {
        var controller = new ReelController(new ReelControlSettings
        {
            ProportionalGain = 1,
            DerivativeGain = 0,
            Deadband = 10,
            LookAheadSeconds = 0
        });

        var decision = controller.Evaluate(105, 100, 0, 0.95, isFresh: true);

        Assert.Equal(ReelCommand.Neutral, decision.Command);
    }

    [Theory]
    [InlineData(0.2, true)]
    [InlineData(0.9, false)]
    public void Evaluate_ReturnsNeutralForStaleOrLowConfidence(double confidence, bool isFresh)
    {
        var controller = new ReelController();

        var decision = controller.Evaluate(200, 100, 100, confidence, isFresh);

        Assert.Equal(ReelCommand.Neutral, decision.Command);
    }

    [Fact]
    public void Evaluate_CanInvertPositiveErrorMapping()
    {
        var controller = new ReelController(new ReelControlSettings
        {
            ProportionalGain = 1,
            DerivativeGain = 0,
            Deadband = 5,
            LookAheadSeconds = 0,
            PositiveErrorMeansHold = false
        });

        var decision = controller.Evaluate(150, 100, 0, 0.95, isFresh: true);

        Assert.Equal(ReelCommand.Release, decision.Command);
    }
}
