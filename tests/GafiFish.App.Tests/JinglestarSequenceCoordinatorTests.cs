using GafiFish.App.Automation;
using GafiFish.Core.Automation;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class JinglestarSequenceCoordinatorTests
{
    [Fact]
    public void ProcessFrame_SendsTenHoldReleasePulsesAndThenYieldsControl()
    {
        var input = new FakeInput();
        var coordinator = new JinglestarSequenceCoordinator(input, 100, 80);
        input.Arm();
        var start = DateTimeOffset.UtcNow;

        Assert.True(coordinator.ProcessFrame(start, FishingState.Reeling));
        Assert.True(input.IsLeftButtonDown);

        var at = start;
        for (var pulse = 0; pulse < JinglestarSequenceCoordinator.RequiredPulses; pulse++)
        {
            at = at.AddMilliseconds(100);
            var ownsInput = coordinator.ProcessFrame(at, FishingState.Reeling);
            Assert.False(input.IsLeftButtonDown);
            if (pulse < JinglestarSequenceCoordinator.RequiredPulses - 1)
            {
                Assert.True(ownsInput);
                at = at.AddMilliseconds(80);
                Assert.True(coordinator.ProcessFrame(at, FishingState.Reeling));
                Assert.True(input.IsLeftButtonDown);
            }
            else
            {
                Assert.False(ownsInput);
            }
        }

        Assert.False(coordinator.IsActive);
        Assert.Equal(10, coordinator.CompletedPulses);
        Assert.False(input.IsLeftButtonDown);
        Assert.Contains("10/10", coordinator.LastStatus);
    }

    [Fact]
    public void ProcessFrame_StartsNextTenPulseSequenceAfterTwoSecondCooldown()
    {
        var input = new FakeInput();
        var coordinator = new JinglestarSequenceCoordinator(input, 100, 100);
        input.Arm();
        var start = DateTimeOffset.UtcNow;

        Assert.True(coordinator.ProcessFrame(start, FishingState.Reeling));
        for (var frame = 1; frame <= 19; frame++)
        {
            _ = coordinator.ProcessFrame(
                start.AddMilliseconds(frame * 100), FishingState.Reeling);
        }

        Assert.False(coordinator.IsActive);
        Assert.Equal(10, coordinator.CompletedPulses);
        var eventsAtEnd = input.Events.Count;

        Assert.False(coordinator.ProcessFrame(
            start.AddMilliseconds(3899), FishingState.Reeling));
        Assert.Equal(eventsAtEnd, input.Events.Count);

        Assert.True(coordinator.ProcessFrame(
            start.AddMilliseconds(3900), FishingState.Reeling));
        Assert.True(input.IsLeftButtonDown);
        Assert.Equal(0, coordinator.CompletedPulses);
    }

    [Fact]
    public void ProcessFrame_DuplicateFrameCannotAdvanceOrRepeatInput()
    {
        var input = new FakeInput();
        var coordinator = new JinglestarSequenceCoordinator(input, 100, 100);
        input.Arm();
        var start = DateTimeOffset.UtcNow;

        Assert.True(coordinator.ProcessFrame(start, FishingState.Reeling));
        var eventCount = input.Events.Count;

        Assert.True(coordinator.ProcessFrame(start, FishingState.Reeling));
        Assert.Equal(eventCount, input.Events.Count);
        Assert.Equal(0, coordinator.CompletedPulses);
    }

    [Fact]
    public void ProcessFrame_LeavingReelingReleasesMouseAndAllowsNextCatch()
    {
        var input = new FakeInput();
        var coordinator = new JinglestarSequenceCoordinator(input, 100, 100);
        input.Arm();
        var start = DateTimeOffset.UtcNow;

        Assert.True(coordinator.ProcessFrame(start, FishingState.Reeling));
        Assert.True(input.IsLeftButtonDown);
        Assert.False(coordinator.ProcessFrame(start.AddMilliseconds(40), FishingState.Preparing));
        Assert.False(input.IsLeftButtonDown);

        Assert.True(coordinator.ProcessFrame(start.AddSeconds(1), FishingState.Reeling));
        Assert.True(input.IsLeftButtonDown);
    }

    [Fact]
    public void ProcessFrame_LossOfFreshMarkersCancelsSequenceAndReleasesMouse()
    {
        var input = new FakeInput();
        var coordinator = new JinglestarSequenceCoordinator(input, 100, 100);
        input.Arm();
        var start = DateTimeOffset.UtcNow;

        Assert.True(coordinator.ProcessFrame(start, FishingState.Reeling));
        Assert.True(input.IsLeftButtonDown);

        Assert.False(coordinator.ProcessFrame(
            start.AddMilliseconds(40), FishingState.Reeling, allowSequence: false));
        Assert.False(input.IsLeftButtonDown);
        Assert.False(coordinator.IsActive);
        Assert.Contains("detections were lost", coordinator.LastStatus);

        Assert.False(coordinator.ProcessFrame(
            start.AddMilliseconds(80), FishingState.Reeling, allowSequence: true));
        Assert.False(input.IsLeftButtonDown);
    }

    [Theory]
    [InlineData(19, 100)]
    [InlineData(100, 501)]
    public void Constructor_RejectsUncalibratedTimingOutsideSafeBounds(int hold, int release)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new JinglestarSequenceCoordinator(new FakeInput(), hold, release));
    }

    private sealed class FakeInput : IRobloxInput
    {
        public bool IsArmed { get; private set; }
        public bool IsLeftButtonDown { get; private set; }
        public List<bool> Events { get; } = [];

        public void Arm() => IsArmed = true;

        public void SetLeftButton(bool isDown)
        {
            if (isDown && !IsArmed)
                throw new InvalidOperationException("Not armed.");
            IsLeftButtonDown = isDown;
            Events.Add(isDown);
        }

        public void SetKey(ushort virtualKey, bool isDown) { }

        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame) { }

        public void Disarm()
        {
            IsArmed = false;
            IsLeftButtonDown = false;
        }
    }
}
