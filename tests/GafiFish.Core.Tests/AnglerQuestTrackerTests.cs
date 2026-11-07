using Xunit;
using GafiFish.Core.Automation;

namespace GafiFish.Core.Tests;

public sealed class AnglerQuestTrackerTests
{
    [Fact]
    public void RequiresTargetFishBeforeItCanBeMarkedCaught()
    {
        var tracker = new AnglerQuestTracker();
        var caughtAt = DateTimeOffset.UtcNow;

        Assert.False(tracker.MarkTargetFishCaught("Trout", caughtAt));
        Assert.False(tracker.HasCatchToSubmit);
    }

    [Fact]
    public void RecognisesTargetIgnoringPunctuationAndCase()
    {
        var tracker = new AnglerQuestTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.StartTarget("Bluefin Tuna", now);

        Assert.True(tracker.MarkTargetFishCaught("BLUEFIN-TUNA!", now.AddSeconds(10)));
        Assert.True(tracker.HasCatchToSubmit);
        Assert.False(tracker.IsTracking);
    }

    [Fact]
    public void CooldownStartsOnlyAfterManualHandInConfirmation()
    {
        var tracker = new AnglerQuestTracker();
        var startedAt = DateTimeOffset.UtcNow;
        tracker.StartTarget("Coelacanth", startedAt);
        tracker.MarkTargetFishCaught("Coelacanth", startedAt.AddMinutes(1));

        Assert.False(tracker.IsInCooldown);
        Assert.False(tracker.CanStartNewQuest(startedAt.AddMinutes(3)));

        var submittedAt = startedAt.AddMinutes(4);
        tracker.ConfirmQuestSubmitted(submittedAt);

        Assert.True(tracker.IsInCooldown);
        Assert.Equal(TimeSpan.FromSeconds(120), tracker.CooldownRemaining(submittedAt));
        Assert.False(tracker.CanStartNewQuest(submittedAt));
        Assert.True(tracker.CanStartNewQuest(submittedAt.AddSeconds(120)));
    }

    [Fact]
    public void CannotStartAnotherTargetBeforeHandInOrCooldown()
    {
        var tracker = new AnglerQuestTracker();
        var now = DateTimeOffset.UtcNow;
        tracker.StartTarget("Megalodon", now);
        tracker.MarkTargetFishCaught("Megalodon", now.AddSeconds(2));

        Assert.Throws<InvalidOperationException>(() =>
            tracker.StartTarget("Colossal Squid", now.AddMinutes(10)));
        tracker.ConfirmQuestSubmitted(now.AddMinutes(10));
        Assert.Throws<InvalidOperationException>(() =>
            tracker.StartTarget("Colossal Squid", now.AddMinutes(11)));

        tracker.StartTarget("Colossal Squid", now.AddMinutes(12));
        Assert.Equal("Colossal Squid", tracker.TargetSpecies);
    }

    [Fact]
    public void RejectsInvalidTargetNames()
    {
        var tracker = new AnglerQuestTracker();
        Assert.Throws<ArgumentException>(() => tracker.StartTarget("  ", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => tracker.StartTarget(new string('x', 101), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Snapshot_RestoresAnActiveRequest()
    {
        var tracker = new AnglerQuestTracker();
        var startedAt = DateTimeOffset.UtcNow;
        tracker.StartTarget("Colossal Squid", startedAt);
        var restored = new AnglerQuestTracker();
        restored.Restore(tracker.CreateSnapshot());
        Assert.True(restored.IsTracking);
        Assert.Equal("Colossal Squid", restored.TargetSpecies);
        Assert.False(restored.HasCatchToSubmit);
    }

    [Fact]
    public void Snapshot_RestoresPendingHandInWithoutStartingCooldown()
    {
        var tracker = new AnglerQuestTracker();
        var caughtAt = DateTimeOffset.UtcNow;
        tracker.StartTarget("Megalodon", caughtAt.AddMinutes(-1));
        tracker.MarkTargetFishCaught("Megalodon", caughtAt);
        var restored = new AnglerQuestTracker();
        restored.Restore(tracker.CreateSnapshot());
        Assert.True(restored.HasCatchToSubmit);
        Assert.False(restored.IsInCooldown);
        Assert.False(restored.CanStartNewQuest(caughtAt.AddHours(1)));
    }

    [Fact]
    public void Snapshot_RestoresSubmittedCooldownAcrossRestart()
    {
        var tracker = new AnglerQuestTracker();
        var startedAt = DateTimeOffset.UtcNow;
        tracker.StartTarget("Ancient Kraken", startedAt);
        tracker.MarkTargetFishCaught("Ancient Kraken", startedAt.AddMinutes(1));
        var submittedAt = startedAt.AddMinutes(2);
        tracker.ConfirmQuestSubmitted(submittedAt);
        var restored = new AnglerQuestTracker();
        restored.Restore(tracker.CreateSnapshot());
        Assert.True(restored.IsInCooldown);
        Assert.Equal(submittedAt.AddSeconds(120), restored.NextRequestAvailableAt);
        Assert.Equal(TimeSpan.FromSeconds(75), restored.CooldownRemaining(submittedAt.AddSeconds(45)));
        Assert.False(restored.CanStartNewQuest(submittedAt.AddSeconds(119)));
        Assert.True(restored.CanStartNewQuest(submittedAt.AddSeconds(120)));
    }

    [Fact]
    public void Restore_RejectsInconsistentSnapshot()
    {
        var tracker = new AnglerQuestTracker();
        var caughtAt = DateTimeOffset.UtcNow;
        var invalid = new AnglerQuestSnapshot("Megalodon", caughtAt, caughtAt.AddSeconds(10), caughtAt.AddSeconds(100));
        Assert.Throws<ArgumentException>(() => tracker.Restore(invalid));
    }
}
