using GafiFish.App.Automation;
using GafiFish.Core.Automation;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class DayNightTotemRotationCoordinatorTests
{
    [Fact]
    public void FirstPhaseSetsBaselineWithoutConsumingATotem()
    {
        var input = new RecordingInput();
        var coordinator = CreateCoordinator(input);

        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Day), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Day), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Day), false));

        Assert.Equal(DayNightPhase.Day, coordinator.BaselinePhase);
        Assert.Empty(input.Events);
        Assert.Contains("baseline", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicatePhaseFramesCannotEstablishBaselineOrUseTotem()
    {
        var input = new RecordingInput();
        var coordinator = CreateCoordinator(input);
        var start = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var day = Observation(DayNightPhase.Day, start);

        Assert.False(coordinator.ProcessObservation(day, false));
        Assert.False(coordinator.ProcessObservation(day, false));
        Assert.Contains("duplicate or out-of-order", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(input.Events);

        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Day, start.AddMilliseconds(120)), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Day, start.AddMilliseconds(240)), false));
        Assert.Equal(DayNightPhase.Day, coordinator.BaselinePhase);
        Assert.Empty(input.Events);

        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Night, start.AddMilliseconds(360)), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Night, start.AddMilliseconds(480)), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Night, start.AddMilliseconds(600)), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Night, start.AddMilliseconds(600)), true));
        Assert.Contains("duplicate or out-of-order", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(input.Events);
        Assert.True(coordinator.ProcessObservation(Observation(DayNightPhase.Night, start.AddMilliseconds(720)), true));
        Assert.Equal(DayNightPhase.Night, coordinator.BaselinePhase);
        Assert.Equal(6, input.Events.Count);
    }

    [Fact]
    public void PhaseChangeWaitsUntilPreparingThenUsesTheMatchingTotemOnce()
    {
        var input = new RecordingInput();
        var coordinator = CreateCoordinator(input);
        SetBaselineDay(coordinator);

        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Night), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Night), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Night), false));
        Assert.Equal(DayNightPhase.Night, coordinator.PendingPhase);
        Assert.Empty(input.Events);

        Assert.True(coordinator.ProcessObservation(Observation(DayNightPhase.Night), true));
        Assert.Equal(DayNightPhase.Night, coordinator.BaselinePhase);
        Assert.Equal(DayNightPhase.Unknown, coordinator.PendingPhase);
        Assert.Equal(
            new[] { "key:52:down", "key:52:up", "mouse:down", "mouse:up", "key:49:down", "key:49:up" },
            input.Events);

        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Night), true));
        Assert.Equal(6, input.Events.Count);
    }

    [Fact]
    public void ReturningToDayUsesTheDaySlotOnlyAfterAnotherStableTransition()
    {
        var input = new RecordingInput();
        var coordinator = CreateCoordinator(input);
        SetBaselineDay(coordinator);
        SetStableNight(coordinator);

        Assert.True(coordinator.ProcessObservation(Observation(DayNightPhase.Night), true));
        input.Events.Clear();

        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Day), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Day), false));
        Assert.False(coordinator.ProcessObservation(Observation(DayNightPhase.Day), false));
        Assert.True(coordinator.ProcessObservation(Observation(DayNightPhase.Day), true));

        Assert.Equal(
            new[] { "key:51:down", "key:51:up", "mouse:down", "mouse:up", "key:49:down", "key:49:up" },
            input.Events);
    }

    [Fact]
    public void UnknownObservationNeverTriggersItemUse()
    {
        var input = new RecordingInput();
        var coordinator = CreateCoordinator(input);
        SetBaselineDay(coordinator);

        var unknown = new DayNightPhaseObservation(DayNightPhase.Unknown, 0, "Indicator not visible.");
        Assert.False(coordinator.ProcessObservation(unknown, true));
        Assert.Empty(input.Events);
    }

    [Fact]
    public void UnknownPhaseAfterAQueuedTransitionClearsThePendingTotem()
    {
        var input = new RecordingInput();
        var coordinator = CreateCoordinator(input);
        SetBaselineDay(coordinator);
        SetStableNight(coordinator);
        Assert.Equal(DayNightPhase.Night, coordinator.PendingPhase);

        var unknown = new DayNightPhaseObservation(DayNightPhase.Unknown, 0, "Indicator disappeared.");
        Assert.False(coordinator.ProcessObservation(unknown, true));
        Assert.Equal(DayNightPhase.Unknown, coordinator.PendingPhase);
        Assert.Empty(input.Events);
    }

    [Fact]
    public void RejectsConflictingHotbarSlots()
    {
        var input = new RecordingInput();
        var hotbar = new HotbarItemCoordinator(input);

        Assert.Throws<ArgumentException>(() => new DayNightTotemRotationCoordinator(hotbar, 1, 3, 3));
        Assert.Throws<ArgumentException>(() => new DayNightTotemRotationCoordinator(hotbar, 1, 1, 3));
    }

    private static DayNightTotemRotationCoordinator CreateCoordinator(RecordingInput input) =>
        new(new HotbarItemCoordinator(input), rodSlot: 1, dayTotemSlot: 3, nightTotemSlot: 4);

    private static void SetBaselineDay(DayNightTotemRotationCoordinator coordinator)
    {
        coordinator.ProcessObservation(Observation(DayNightPhase.Day), false);
        coordinator.ProcessObservation(Observation(DayNightPhase.Day), false);
        coordinator.ProcessObservation(Observation(DayNightPhase.Day), false);
    }

    private static void SetStableNight(DayNightTotemRotationCoordinator coordinator)
    {
        coordinator.ProcessObservation(Observation(DayNightPhase.Night), false);
        coordinator.ProcessObservation(Observation(DayNightPhase.Night), false);
        coordinator.ProcessObservation(Observation(DayNightPhase.Night), false);
    }

    private static DayNightPhaseObservation Observation(DayNightPhase phase, DateTimeOffset? capturedAt = null) =>
        new(phase, 0.95, $"{phase} phase indicator detected.", capturedAt);

    private sealed class RecordingInput : IRobloxInput
    {
        public List<string> Events { get; } = [];
        public bool IsArmed { get; private set; }
        public void Arm() => IsArmed = true;
        public void SetLeftButton(bool isDown) => Events.Add(isDown ? "mouse:down" : "mouse:up");
        public void SetKey(ushort key, bool isDown) => Events.Add($"key:{key}:{(isDown ? "down" : "up")}");
        public void ClickClient(int x, int y, ScreenCaptureFrame frame) => throw new NotSupportedException();
        public void Disarm() => IsArmed = false;
    }
}
