using GafiFish.Core.Automation;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class FishingStateMachineTests
{
    [Fact]
    public void StateMachine_RejectsSkippingFishingPhases()
    {
        var machine = new FishingStateMachine();

        Assert.False(machine.TryTransition(FishingState.Reeling, "Skip directly to reel."));
        Assert.Equal(FishingState.Idle, machine.CurrentState);
    }

    [Fact]
    public void StateMachine_AllowsNormalFishingCycle()
    {
        var machine = new FishingStateMachine();
        var now = DateTimeOffset.UtcNow;

        Assert.True(machine.TryTransition(FishingState.Preparing, "Session started.", now));
        Assert.True(machine.TryTransition(FishingState.Casting, "Rod cast.", now));
        Assert.True(machine.TryTransition(FishingState.WaitingForBite, "Bobber landed.", now));
        Assert.True(machine.TryTransition(FishingState.Shaking, "Bite prompt detected.", now));
        Assert.True(machine.TryTransition(FishingState.Reeling, "Shake completed.", now));
        Assert.True(machine.TryTransition(FishingState.CatchDetected, "Catch result detected.", now));

        Assert.Equal(FishingState.CatchDetected, machine.CurrentState);
    }

    [Fact]
    public void StateMachine_AllowsVisualMarkersToEnterReelingFromPreparing()
    {
        var machine = new FishingStateMachine();

        Assert.True(machine.TryTransition(FishingState.Preparing, "Automation armed."));
        Assert.True(machine.TryTransition(FishingState.Reeling, "Both calibrated reel markers are visible."));
        Assert.Equal(FishingState.Reeling, machine.CurrentState);
    }

    [Fact]
    public void StateMachine_AllowsShakeFromPreparingAndReelFromWaitingForBite()
    {
        var machine = new FishingStateMachine();

        Assert.True(machine.TryTransition(FishingState.Preparing, "Automation armed."));
        Assert.True(machine.TryTransition(FishingState.Shaking, "A calibrated prompt appeared."));
        Assert.True(machine.TryTransition(FishingState.Reeling, "Both reel markers appeared."));
        Assert.Equal(FishingState.Reeling, machine.CurrentState);
    }

    [Fact]
    public void StateMachine_AllowsReelAfterCastWaitsForBite()
    {
        var machine = new FishingStateMachine();

        Assert.True(machine.TryTransition(FishingState.Preparing, "Automation armed."));
        Assert.True(machine.TryTransition(FishingState.Casting, "Cast started."));
        Assert.True(machine.TryTransition(FishingState.WaitingForBite, "Cast released."));
        Assert.True(machine.TryTransition(FishingState.Reeling, "Both reel markers appeared."));
        Assert.Equal(FishingState.Reeling, machine.CurrentState);
    }

    [Fact]
    public void StateMachine_AllowsOcrCatchNotificationToMarkCatchDetected()
    {
        var machine = new FishingStateMachine();

        Assert.True(machine.TryTransition(FishingState.Preparing, "Automation armed."));
        Assert.True(machine.TryTransition(FishingState.CatchDetected, "OCR confirmed a catch."));
        Assert.Equal(FishingState.CatchDetected, machine.CurrentState);
    }

    [Fact]
    public void StateMachine_RequiresSaleVerificationBeforeReturningToFishing()
    {
        var machine = new FishingStateMachine();

        machine.TryTransition(FishingState.Preparing, "Start.");
        machine.TryTransition(FishingState.Casting, "Cast.");
        machine.TryTransition(FishingState.WaitingForBite, "Bite wait.");
        machine.TryTransition(FishingState.Shaking, "Shake.");
        machine.TryTransition(FishingState.Reeling, "Reel.");
        machine.TryTransition(FishingState.CatchDetected, "Catch.");
        machine.TryTransition(FishingState.SellPending, "Ten-minute sale due.");
        machine.TryTransition(FishingState.OpeningInventory, "Fishing cycle ended.");
        machine.TryTransition(FishingState.SellingInventory, "Sell button recognised.");
        machine.TryTransition(FishingState.VerifyingSale, "Sell button activated.");
        machine.TryTransition(FishingState.ClosingInventory, "Sale confirmed.");

        Assert.True(machine.TryTransition(FishingState.Preparing, "Inventory closed."));
        Assert.Equal(FishingState.Preparing, machine.CurrentState);
    }

    [Fact]
    public void StateMachine_RequiresStoppingBeforeStopped()
    {
        var machine = new FishingStateMachine();

        Assert.False(machine.TryTransition(FishingState.Stopped, "Direct stop."));
        Assert.True(machine.TryTransition(FishingState.Stopping, "User requested stop."));
        Assert.True(machine.TryTransition(FishingState.Stopped, "Inputs released."));
    }

    [Fact]
    public void StateMachine_ReportsStateTimeout()
    {
        var start = DateTimeOffset.UtcNow;
        var machine = new FishingStateMachine();
        machine.TryTransition(FishingState.Preparing, "Start.", start);

        Assert.False(machine.HasTimedOut(TimeSpan.FromSeconds(5), start.AddSeconds(4)));
        Assert.True(machine.HasTimedOut(TimeSpan.FromSeconds(5), start.AddSeconds(6)));
    }
}
