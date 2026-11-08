using GafiFish.App.Automation;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class DredPassiveTrackerTests
{
    [Fact]
    public void Process_StartsFourSecondCadenceAndLocksForSixHundredMilliseconds()
    {
        var tracker = new DredPassiveTracker();
        var start = DateTimeOffset.UtcNow;

        var baseline = tracker.Process(start);
        Assert.False(baseline.IsLocked);
        Assert.Equal(1, (baseline.NextChargeAt!.Value - start).TotalSeconds / 4d);

        var before = tracker.Process(start.AddMilliseconds(3999));
        Assert.False(before.IsLocked);
        Assert.Equal(0, before.ChargeCount);

        var trigger = tracker.Process(start.AddSeconds(4));
        Assert.True(trigger.IsLocked);
        Assert.False(trigger.IsOverdrive);
        Assert.Equal(1, trigger.ChargeCount);

        var stillLocked = tracker.Process(start.AddMilliseconds(4599));
        Assert.True(stillLocked.IsLocked);

        var unlocked = tracker.Process(start.AddMilliseconds(4600));
        Assert.False(unlocked.IsLocked);
        Assert.Equal(1, unlocked.ChargeCount);
    }

    [Fact]
    public void Process_EntersOverdriveOnThirdChargeAndResetsChargeCount()
    {
        var tracker = new DredPassiveTracker();
        var start = DateTimeOffset.UtcNow;
        _ = tracker.Process(start);

        _ = tracker.Process(start.AddSeconds(4));
        _ = tracker.Process(start.AddSeconds(8));
        var overdrive = tracker.Process(start.AddSeconds(12));

        Assert.True(overdrive.IsLocked);
        Assert.True(overdrive.IsOverdrive);
        Assert.Equal(0, overdrive.ChargeCount);
        Assert.Equal(1, tracker.OverdriveActivations);

        Assert.True(tracker.Process(start.AddMilliseconds(14499)).IsLocked);
        Assert.False(tracker.Process(start.AddMilliseconds(14500)).IsLocked);
    }

    [Fact]
    public void Process_IgnoresDuplicateAndOutOfOrderFrames()
    {
        var tracker = new DredPassiveTracker();
        var start = DateTimeOffset.UtcNow;

        _ = tracker.Process(start);
        var first = tracker.Process(start.AddSeconds(4));
        var duplicate = tracker.Process(start.AddSeconds(4));
        var older = tracker.Process(start.AddSeconds(3));

        Assert.True(first.IsLocked);
        Assert.True(duplicate.IsLocked);
        Assert.True(older.IsLocked);
        Assert.Equal(1, tracker.ChargeCount);
    }

    [Fact]
    public void Reset_RestartsCadenceForNextFish()
    {
        var tracker = new DredPassiveTracker();
        var start = DateTimeOffset.UtcNow;
        _ = tracker.Process(start);
        _ = tracker.Process(start.AddSeconds(4));

        tracker.Reset();
        var next = tracker.Process(start.AddMinutes(1));

        Assert.False(next.IsLocked);
        Assert.Equal(0, next.ChargeCount);
        Assert.Equal(start.AddMinutes(1).AddSeconds(4), next.NextChargeAt);
    }
}
