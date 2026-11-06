using GafiFish.Core.Aquarium;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class AquariumReadyAlertTrackerTests
{
    [Fact]
    public void EmitsEachReadyFoodActionOnce()
    {
        var now = new DateTimeOffset(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);
        var state = new AquariumScheduleState
        {
            GuppiesBest = new AquariumFoodProgress(now.AddHours(-1), now.AddHours(-2)),
            FishChow = new AquariumFoodProgress(now.AddHours(-1), null)
        };
        var tracker = new AquariumReadyAlertTracker();

        var first = tracker.Evaluate(state, now);
        var second = tracker.Evaluate(state, now.AddSeconds(1));

        Assert.Equal(3, first.Count);
        Assert.Contains("Guppies' Best · feeding", first);
        Assert.Contains("Guppies' Best · restock", first);
        Assert.Contains("Fish Chow · feeding", first);
        Assert.Empty(second);
    }

    [Fact]
    public void NewCompletionTimestampCanAlertAgain()
    {
        var now = new DateTimeOffset(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);
        var tracker = new AquariumReadyAlertTracker();
        var first = new AquariumScheduleState
        {
            GuppiesBest = new AquariumFoodProgress(LastFedAt: now.AddHours(-1))
        };
        var later = first with
        {
            GuppiesBest = first.GuppiesBest with { LastFedAt = now.AddHours(-2) }
        };

        Assert.Single(tracker.Evaluate(first, now));
        Assert.Single(tracker.Evaluate(later, now));
    }

    [Fact]
    public void ClaimAlertsRespectClaimReminderSetting()
    {
        var now = new DateTimeOffset(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);
        var state = new AquariumScheduleState
        {
            LastClaimedAt = now.AddHours(-1),
            ClaimReminderEnabled = false
        };
        var tracker = new AquariumReadyAlertTracker();

        Assert.Empty(tracker.Evaluate(state, now));
        Assert.Contains("Aquarium reward claim", tracker.Evaluate(
            state with { ClaimReminderEnabled = true }, now));
    }

    [Fact]
    public void UntrackedActionsNeverTriggerAlerts()
    {
        var tracker = new AquariumReadyAlertTracker();

        Assert.Empty(tracker.Evaluate(new AquariumScheduleState(), DateTimeOffset.UtcNow));
    }
}
