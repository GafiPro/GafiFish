using GafiFish.Core.Scheduling;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class TimedHotbarActionScheduleTests
{
    [Fact]
    public void ActionIsDueOnlyAfterInterval()
    {
        var start = DateTimeOffset.UtcNow;
        var action = new TimedHotbarAction(TimedHotbarActionKind.Totem, 3, TimeSpan.FromMinutes(10));
        var schedule = new TimedHotbarActionSchedule([action], start);
        Assert.Null(schedule.TryGetDue(start.AddMinutes(9), true));
        Assert.Equal(action, schedule.TryGetDue(start.AddMinutes(10), true));
    }

    [Fact]
    public void UnsafeStateDoesNotReturnAction()
    {
        var start = DateTimeOffset.UtcNow;
        var action = new TimedHotbarAction(TimedHotbarActionKind.Potion, 5, TimeSpan.FromMinutes(1));
        var schedule = new TimedHotbarActionSchedule([action], start);
        Assert.Null(schedule.TryGetDue(start.AddMinutes(2), false));
        Assert.Equal(action, schedule.TryGetDue(start.AddMinutes(2), true));
    }

    [Fact]
    public void SimultaneouslyDueActionsAreSeparatedByMinimumGap()
    {
        var start = DateTimeOffset.UtcNow;
        var totem = new TimedHotbarAction(TimedHotbarActionKind.Totem, 3, TimeSpan.FromMinutes(1));
        var potion = new TimedHotbarAction(TimedHotbarActionKind.Potion, 5, TimeSpan.FromMinutes(1));
        var schedule = new TimedHotbarActionSchedule([totem, potion], start);
        var used = start.AddMinutes(1);

        var firstAction = schedule.TryGetDue(used, isSafeState: true);
        Assert.Equal(totem, firstAction);
        schedule.MarkExecuted(firstAction!, used);

        Assert.Null(schedule.TryGetDue(used.AddSeconds(1), isSafeState: true));
        Assert.Equal(potion, schedule.TryGetDue(used.AddSeconds(2), isSafeState: true));
    }

    [Fact]
    public void InvalidSettingsAndDuplicateKindsAreRejected()
    {
        var start = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => new TimedHotbarActionSchedule(
            [new TimedHotbarAction(TimedHotbarActionKind.Totem, 3, TimeSpan.FromMinutes(10)),
             new TimedHotbarAction(TimedHotbarActionKind.Totem, 4, TimeSpan.FromMinutes(11))], start));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimedHotbarActionSchedule(
            [new TimedHotbarAction(TimedHotbarActionKind.Potion, 12, TimeSpan.FromMinutes(10))], start));
    }
}
