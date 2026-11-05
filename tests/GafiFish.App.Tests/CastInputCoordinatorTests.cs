using GafiFish.App.Automation;
using GafiFish.Core.Casting;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class CastInputCoordinatorTests
{
    [Fact]
    public void ProcessObservation_ReleasesMouseInsideCalibratedTarget()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new CastInputCoordinator(input);
        var now = DateTimeOffset.UtcNow;

        coordinator.BeginCast(now);
        Assert.True(input.IsLeftButtonDown);

        var result = coordinator.ProcessObservation(
            Observation(position: 0.55, targetStart: 0.50, targetEnd: 0.60, capturedAt: now.AddMilliseconds(20)),
            now.AddMilliseconds(20));

        Assert.Equal(CastInputResultKind.Released, result.Kind);
        Assert.False(input.IsLeftButtonDown);
        Assert.False(coordinator.IsHolding);
    }

    [Fact]
    public void ProcessObservation_WaitsWhenIndicatorHasNotReachedTarget()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new CastInputCoordinator(input);
        var now = DateTimeOffset.UtcNow;

        coordinator.BeginCast(now);

        var result = coordinator.ProcessObservation(
            Observation(position: 0.20, targetStart: 0.50, targetEnd: 0.60, capturedAt: now.AddMilliseconds(20)),
            now.AddMilliseconds(20));

        Assert.Equal(CastInputResultKind.WaitingForTarget, result.Kind);
        Assert.True(input.IsLeftButtonDown);
        Assert.True(coordinator.IsHolding);

        coordinator.Stop();
        Assert.False(input.IsLeftButtonDown);
    }

    [Fact]
    public void ProcessObservation_TimesOutAndReleasesWhenMeterNeverAppears()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new CastInputCoordinator(
            input,
            maximumHoldDuration: TimeSpan.FromSeconds(1));
        var now = DateTimeOffset.UtcNow;

        coordinator.BeginCast(now);

        var result = coordinator.ProcessObservation(
            observation: null,
            now.AddSeconds(2));

        Assert.Equal(CastInputResultKind.TimedOut, result.Kind);
        Assert.False(input.IsLeftButtonDown);
        Assert.False(coordinator.IsHolding);
    }

    [Fact]
    public void ProcessObservation_CancelsAndReleasesStaleMeasurement()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new CastInputCoordinator(input);
        var now = DateTimeOffset.UtcNow;

        coordinator.BeginCast(now);

        var result = coordinator.ProcessObservation(
            Observation(
                position: 0.55,
                targetStart: 0.50,
                targetEnd: 0.60,
                capturedAt: now.AddSeconds(-2)),
            now);

        Assert.Equal(CastInputResultKind.Cancelled, result.Kind);
        Assert.False(input.IsLeftButtonDown);
        Assert.False(coordinator.IsHolding);
    }

    private static CastMeterObservation Observation(
        double position,
        double targetStart,
        double targetEnd,
        DateTimeOffset capturedAt) =>
        new(
            true,
            position,
            targetStart,
            targetEnd,
            IndicatorVelocityPerSecond: 0,
            Confidence: 0.99,
            CapturedAt: capturedAt);

    private sealed class FakeRobloxInput : IRobloxInput
    {
        public bool IsArmed { get; private set; }
        public bool IsLeftButtonDown { get; private set; }

        public void Arm() => IsArmed = true;

        public void SetLeftButton(bool isDown)
        {
            EnsureArmed();
            IsLeftButtonDown = isDown;
        }

        public void SetKey(ushort virtualKey, bool isDown) => EnsureArmed();

        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame) => EnsureArmed();

        public void Disarm()
        {
            IsArmed = false;
            IsLeftButtonDown = false;
        }

        private void EnsureArmed()
        {
            if (!IsArmed)
            {
                throw new InvalidOperationException("Fake input is disarmed.");
            }
        }
    }
}
