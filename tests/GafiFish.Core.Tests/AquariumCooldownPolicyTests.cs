using GafiFish.Core.Aquarium;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class AquariumCooldownPolicyTests
{
    [Fact]
    public void CooldownIsNotTrackedUntilAnActionIsRecorded()
    {
        var result = AquariumCooldownPolicy.Evaluate(null, DateTimeOffset.UtcNow);

        Assert.Equal(AquariumCooldownStatus.NotTracked, result.Status);
        Assert.Null(result.NextEligibleAt);
        Assert.Null(result.Remaining);
    }

    [Fact]
    public void CooldownRemainsActiveUntilOneHourHasElapsed()
    {
        var lastAction = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

        var beforeBoundary = AquariumCooldownPolicy.Evaluate(
            lastAction,
            lastAction.AddMinutes(59).AddSeconds(59));
        var atBoundary = AquariumCooldownPolicy.Evaluate(
            lastAction,
            lastAction.AddHours(1));

        Assert.Equal(AquariumCooldownStatus.CoolingDown, beforeBoundary.Status);
        Assert.Equal(TimeSpan.FromSeconds(1), beforeBoundary.Remaining);
        Assert.Equal(AquariumCooldownStatus.Ready, atBoundary.Status);
        Assert.Equal(TimeSpan.Zero, atBoundary.Remaining);
    }

    [Fact]
    public void FeedingAndRestockingAreTrackedIndependentlyPerFood()
    {
        var start = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        var state = new AquariumScheduleState()
            .MarkFed(AquariumFoodType.GuppiesBest, start)
            .MarkRestocked(AquariumFoodType.FishChow, start.AddMinutes(5));

        Assert.Equal(start, state.GuppiesBest.LastFedAt);
        Assert.Null(state.GuppiesBest.LastRestockedAt);
        Assert.Equal(start.AddMinutes(5), state.FishChow.LastRestockedAt);
        Assert.Null(state.FishChow.LastFedAt);
        Assert.Null(state.SplashSnacks.LastFedAt);
    }

    [Fact]
    public void MarkingOneFoodDoesNotChangeTheOtherFoodRecords()
    {
        var now = DateTimeOffset.UtcNow;
        var originalFishChow = new AquariumFoodProgress(now.AddHours(-2), now.AddHours(-1));
        var state = new AquariumScheduleState { FishChow = originalFishChow };

        var updated = state.MarkFed(AquariumFoodType.SplashSnacks, now);

        Assert.Equal(originalFishChow, updated.FishChow);
        Assert.Equal(now, updated.SplashSnacks.LastFedAt);
    }
}
