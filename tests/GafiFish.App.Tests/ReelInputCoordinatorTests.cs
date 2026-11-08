using GafiFish.App.Automation;
using GafiFish.Core.Automation;
using GafiFish.Core.Control;
using GafiFish.Vision.Detection;
using GafiFish.Vision.Tracking;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class ReelInputCoordinatorTests
{
    [Fact]
    public void Apply_HoldsMouseWhenPredictiveControllerRequestsHold()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var decision = coordinator.Apply(
            FishingState.Reeling,
            Marker(150),
            Marker(100),
            Motion(150, now));

        Assert.Equal(ReelCommand.Hold, decision.Command);
        Assert.True(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_AccountsForControlBarMovingAcrossTheScreen()
    {
        var input = new FakeRobloxInput();
        var coordinator = new ReelInputCoordinator(
            input,
            new ReelController(new ReelControlSettings
            {
                ProportionalGain = 0,
                DerivativeGain = 1,
                Deadband = 0,
                LookAheadSeconds = 0,
                MinimumConfidence = 0.65
            }));
        coordinator.Arm();

        var start = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        _ = coordinator.Apply(
            FishingState.Reeling,
            fish,
            Marker(100),
            Motion(150, start));

        var decision = coordinator.Apply(
            FishingState.Reeling,
            fish,
            Marker(120),
            Motion(150, start.AddMilliseconds(100)));

        Assert.Equal(ReelCommand.Release, decision.Command);
        Assert.False(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_ReleasesWhenControlBarObservationTimestampIsRepeated()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        _ = coordinator.Apply(FishingState.Reeling, Marker(150), Marker(100), Motion(150, now));
        var duplicate = coordinator.Apply(
            FishingState.Reeling, Marker(150), Marker(100), Motion(150, now));

        Assert.Equal(ReelCommand.Neutral, duplicate.Command);
        Assert.False(input.IsLeftButtonDown);
        Assert.Contains("fresh control-bar", duplicate.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_ReleasesMouseWhenMarkersDisappear()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        _ = coordinator.Apply(
            FishingState.Reeling,
            Marker(150),
            Marker(100),
            Motion(150, now));

        var decision = coordinator.Apply(
            FishingState.Reeling,
            null,
            Marker(100),
            Motion(150, now.AddMilliseconds(100)));

        Assert.Equal(ReelCommand.Neutral, decision.Command);
        Assert.False(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_MaintainsPreviousMouseStateInsideDeadband()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 10);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        _ = coordinator.Apply(
            FishingState.Reeling,
            Marker(150),
            Marker(100),
            Motion(150, now));

        var decision = coordinator.Apply(
            FishingState.Reeling,
            Marker(103),
            Marker(100),
            Motion(103, now.AddMilliseconds(100)));

        Assert.Equal(ReelCommand.Maintain, decision.Command);
        Assert.True(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_ReleasesMouseOutsideReelingState()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        _ = coordinator.Apply(
            FishingState.Reeling,
            Marker(150),
            Marker(100),
            Motion(150, now));

        var decision = coordinator.Apply(
            FishingState.Preparing,
            Marker(150),
            Marker(100),
            Motion(150, now.AddMilliseconds(100)));

        Assert.Equal(ReelCommand.Neutral, decision.Command);
        Assert.False(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_ReleasesChargeAtEightyPercentWhenFishIsInsideTheControlBar()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        var outsideBar = new DetectedMarker(
            100, 40, new OpenCvSharp.Rect(70, 37, 40, 6), 240, 0.95);
        var held = coordinator.Apply(
            FishingState.Reeling, fish, outsideBar, Motion(150, now));
        Assert.Equal(ReelCommand.Hold, held.Command);
        Assert.True(input.IsLeftButtonDown);

        var barContainingFish = new DetectedMarker(
            130, 40, new OpenCvSharp.Rect(100, 37, 100, 6), 600, 0.95);
        var release = coordinator.Apply(
            FishingState.Reeling,
            fish,
            barContainingFish,
            Motion(150, now.AddMilliseconds(100)),
            new RodChargeMeterObservation(now.AddMilliseconds(100), 0.85, 0.95, "Charge 85%."));

        Assert.Equal(ReelCommand.Release, release.Command);
        Assert.False(input.IsLeftButtonDown);
        Assert.Contains("charge", release.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_ReleasesOnlyOncePerChargeCycleAndRearmsAfterLowReading()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var start = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        var barContainingFish = new DetectedMarker(
            150, 40, new OpenCvSharp.Rect(100, 37, 100, 6), 600, 0.95);

        var firstRelease = coordinator.Apply(
            FishingState.Reeling, fish, barContainingFish, Motion(150, start),
            new RodChargeMeterObservation(start, 0.85, 0.95, "Charge 85%."));
        Assert.Contains("one release", firstRelease.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(input.IsLeftButtonDown);

        var stillHigh = coordinator.Apply(
            FishingState.Reeling, fish, barContainingFish, Motion(150, start.AddMilliseconds(100)),
            new RodChargeMeterObservation(start.AddMilliseconds(100), 0.92, 0.95, "Charge 92%."));
        Assert.DoesNotContain("one release", stillHigh.Reason, StringComparison.OrdinalIgnoreCase);

        var drained = coordinator.Apply(
            FishingState.Reeling, fish, barContainingFish, Motion(150, start.AddMilliseconds(200)),
            new RodChargeMeterObservation(start.AddMilliseconds(200), 0.15, 0.95, "Charge 15%."));
        Assert.DoesNotContain("one release", drained.Reason, StringComparison.OrdinalIgnoreCase);

        var nextCycle = coordinator.Apply(
            FishingState.Reeling, fish, barContainingFish, Motion(150, start.AddMilliseconds(300)),
            new RodChargeMeterObservation(start.AddMilliseconds(300), 0.88, 0.95, "Charge 88%."));
        Assert.Contains("one release", nextCycle.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_DoesNotConsumeChargeCycleUntilFishIsInsideTheBar()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var start = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        var narrowBar = new DetectedMarker(
            100, 40, new OpenCvSharp.Rect(70, 37, 40, 6), 240, 0.95);

        var outside = coordinator.Apply(
            FishingState.Reeling, fish, narrowBar, Motion(150, start),
            new RodChargeMeterObservation(start, 0.90, 0.95, "Charge 90%."));
        Assert.DoesNotContain("one release", outside.Reason, StringComparison.OrdinalIgnoreCase);

        var wideBar = new DetectedMarker(
            130, 40, new OpenCvSharp.Rect(100, 37, 100, 6), 600, 0.95);
        var inside = coordinator.Apply(
            FishingState.Reeling, fish, wideBar, Motion(150, start.AddMilliseconds(100)),
            new RodChargeMeterObservation(start.AddMilliseconds(100), 0.91, 0.95, "Charge 91%."));
        Assert.Contains("one release", inside.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_DoesNotReleaseChargeWhenFishIsOutsideRotatedBarCrossSection()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var fishOutsideRotatedSlice = new DetectedMarker(
            150, 80, new OpenCvSharp.Rect(147, 77, 6, 6), 25, 0.95);
        var rotatedBar = new DetectedMarker(
            130, 40, new OpenCvSharp.Rect(90, 0, 80, 80), 600, 0.95)
        {
            OrientedBounds = new OpenCvSharp.RotatedRect(
                new OpenCvSharp.Point2f(130, 40),
                new OpenCvSharp.Size2f(100, 8),
                45)
        };

        var decision = coordinator.Apply(
            FishingState.Reeling,
            fishOutsideRotatedSlice,
            rotatedBar,
            Motion(150, now),
            new RodChargeMeterObservation(now, 0.90, 0.95, "Charge 90%."));

        Assert.DoesNotContain("one release", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ReelCommand.Neutral, decision.Command);
        Assert.False(input.IsLeftButtonDown);
        Assert.True(input.IsArmed);
    }

    [Fact]
    public void Apply_UsesRotatedBarSliceWhenFishIsInsideIt()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        var rotatedBar = new DetectedMarker(
            130, 40, new OpenCvSharp.Rect(90, 0, 80, 80), 600, 0.95)
        {
            OrientedBounds = new OpenCvSharp.RotatedRect(
                new OpenCvSharp.Point2f(130, 40),
                new OpenCvSharp.Size2f(100, 8),
                45)
        };

        var decision = coordinator.Apply(
            FishingState.Reeling,
            fish,
            rotatedBar,
            Motion(150, now),
            new RodChargeMeterObservation(now, 0.90, 0.95, "Charge 90%."));

        Assert.Contains("one release", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_DoesNotUseStaleChargeMeterObservation()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        var barContainingFish = new DetectedMarker(
            150, 40, new OpenCvSharp.Rect(100, 37, 100, 6), 600, 0.95);

        var decision = coordinator.Apply(
            FishingState.Reeling,
            fish,
            barContainingFish,
            Motion(150, now),
            new RodChargeMeterObservation(now.AddMilliseconds(-250), 0.95, 0.95, "Stale charge."));

        Assert.DoesNotContain("charge", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_DoesNotUseFutureChargeMeterObservation()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        var barContainingFish = new DetectedMarker(
            150, 40, new OpenCvSharp.Rect(100, 37, 100, 6), 600, 0.95);

        var decision = coordinator.Apply(
            FishingState.Reeling,
            fish,
            barContainingFish,
            Motion(150, now),
            new RodChargeMeterObservation(now.AddMilliseconds(20), 0.95, 0.95, "Future charge."));

        Assert.DoesNotContain("charge", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_DoesNotUseChargeReleaseWhenFishIsOutsideTheControlBar()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        var outsideBar = new DetectedMarker(
            110, 40, new OpenCvSharp.Rect(90, 37, 40, 6), 240, 0.95);
        var decision = coordinator.Apply(
            FishingState.Reeling,
            fish,
            outsideBar,
            Motion(150, now),
            new RodChargeMeterObservation(now, 0.90, 0.95, "Charge 90%."));

        Assert.Equal(ReelCommand.Hold, decision.Command);
        Assert.True(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_DoesNotUseChargeReleaseForAnUnusableMeterObservation()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var fish = Marker(150);
        var control = Marker(100);
        var decision = coordinator.Apply(
            FishingState.Reeling,
            fish,
            control,
            Motion(150, now),
            new RodChargeMeterObservation(now, 0.95, 0.2, "Uncertain charge reading."));

        Assert.Equal(ReelCommand.Hold, decision.Command);
        Assert.True(input.IsLeftButtonDown);
    }

    [Fact]
    public void Apply_ReleasesWhenTwoFishCannotFitInsideTheControlBar()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();
        var now = DateTimeOffset.UtcNow;
        var combinedFish = new DetectedMarker(150, 40, new OpenCvSharp.Rect(100, 37, 70, 6), 350, 0.95);
        var narrowBar = new DetectedMarker(150, 40, new OpenCvSharp.Rect(130, 37, 40, 6), 240, 0.95);
        var decision = coordinator.Apply(
            FishingState.Reeling, combinedFish, narrowBar, Motion(150, now),
            requireDualFishCoverage: true);
        Assert.Equal(ReelCommand.Neutral, decision.Command);
        Assert.False(input.IsLeftButtonDown);
        Assert.Contains("too far apart", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_DualFishCoverageGuardAllowsBothCentresWhenBarIsWideEnough()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        var fishTargets = new DetectedMarker(
            50, 40, new OpenCvSharp.Rect(40, 37, 20, 6), 120, 0.95);
        var controlBar = new DetectedMarker(
            50, 40, new OpenCvSharp.Rect(40, 37, 20, 6), 120, 0.95);

        var decision = coordinator.Apply(
            FishingState.Reeling,
            fishTargets,
            controlBar,
            Motion(50, now),
            requireDualFishCoverage: true);

        Assert.DoesNotContain("too far apart", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(input.IsArmed);
    }

    [Fact]
    public void Stop_DisarmsAndReleasesAllInputs()
    {
        var input = new FakeRobloxInput();
        var coordinator = CreateCoordinator(input, deadband: 0);
        coordinator.Arm();

        var now = DateTimeOffset.UtcNow;
        _ = coordinator.Apply(
            FishingState.Reeling,
            Marker(150),
            Marker(100),
            Motion(150, now));

        coordinator.Stop();

        Assert.False(input.IsArmed);
        Assert.False(input.IsLeftButtonDown);
    }

    private static ReelInputCoordinator CreateCoordinator(
        FakeRobloxInput input,
        double deadband) =>
        new(
            input,
            new ReelController(new ReelControlSettings
            {
                ProportionalGain = 1,
                DerivativeGain = 0,
                Deadband = deadband,
                LookAheadSeconds = 0,
                MinimumConfidence = 0.65
            }));

    private static DetectedMarker Marker(double x) =>
        new(x, 40, new OpenCvSharp.Rect((int)x - 3, 37, 6, 6), 25, 0.95);

    private static FishMotionEstimate Motion(double x, DateTimeOffset time) =>
        new(x, 0, x, 0.95, time, IsFresh: true);

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

        public void SetKey(ushort virtualKey, bool isDown)
        {
            EnsureArmed();
        }

        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame)
        {
            EnsureArmed();
        }

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
