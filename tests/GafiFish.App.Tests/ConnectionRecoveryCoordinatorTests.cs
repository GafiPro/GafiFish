using GafiFish.App.Automation;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class ConnectionRecoveryCoordinatorTests
{
    [Fact]
    public void ClicksOnceAfterThreeStableFramesAndWaitsForDismissal()
    {
        var start = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = CreateCoordinator(input);
        var profile = CreateProfile();

        coordinator.Start();
        coordinator.ProcessFrame(CreateFrame(start, true), profile);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile);
        Assert.Equal(0, input.Clicks);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile);
        Assert.Equal(1, input.Clicks);
        Assert.Equal(1, coordinator.AttemptsMade);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(360), false), profile);
        Assert.True(coordinator.IsStarted);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(480), false), profile);

        Assert.True(coordinator.IsStarted);
        Assert.Contains("not proof", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, input.Clicks);
        coordinator.Stop();
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void DuplicateFrameCannotSatisfyReconnectStabilityThreshold()
    {
        var start = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = CreateCoordinator(input);
        var profile = CreateProfile();
        var first = CreateFrame(start, true);

        coordinator.Start();
        coordinator.ProcessFrame(first, profile);
        coordinator.ProcessFrame(first, profile);
        Assert.Equal(0, input.Clicks);
        Assert.Contains("duplicate or out-of-order", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile);
        Assert.Equal(0, input.Clicks);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile);
        Assert.Equal(1, input.Clicks);
        coordinator.Stop();
    }

    [Fact]
    public void DoesNotRetryUntilPromptDisappearsAndCooldownExpires()
    {
        var start = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = CreateCoordinator(input, maximumAttempts: 2, retryDelay: TimeSpan.FromSeconds(2));
        var profile = CreateProfile();

        coordinator.Start();
        SendStableButton(coordinator, profile, start);
        Assert.Equal(1, input.Clicks);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(360), false), profile);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(480), false), profile);
        SendStableButton(coordinator, profile, start.AddSeconds(1));
        Assert.Equal(1, input.Clicks);

        SendStableButton(coordinator, profile, start.AddSeconds(3));
        Assert.Equal(2, input.Clicks);

        coordinator.ProcessFrame(CreateFrame(start.AddSeconds(3).AddMilliseconds(120), false), profile);
        coordinator.ProcessFrame(CreateFrame(start.AddSeconds(3).AddMilliseconds(240), false), profile);

        Assert.False(coordinator.IsStarted);
        Assert.Equal(2, input.Clicks);
        Assert.Contains("limit", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void StopsWithoutRetryWhenButtonDoesNotDisappearAfterClick()
    {
        var start = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = CreateCoordinator(input, phaseTimeout: TimeSpan.FromSeconds(1));
        var profile = CreateProfile();

        coordinator.Start();
        SendStableButton(coordinator, profile, start);
        coordinator.ProcessFrame(CreateFrame(start.AddSeconds(2), true), profile);

        Assert.False(coordinator.IsStarted);
        Assert.Equal(1, input.Clicks);
        Assert.False(input.IsArmed);
        Assert.Contains("No retry", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StopAlwaysReleasesInput()
    {
        var input = new FakeInput();
        var coordinator = CreateCoordinator(input);
        coordinator.Start();
        coordinator.Stop();
        Assert.False(input.IsArmed);
    }

    private static ConnectionRecoveryCoordinator CreateCoordinator(
        FakeInput input,
        int maximumAttempts = 3,
        TimeSpan? phaseTimeout = null,
        TimeSpan? retryDelay = null) =>
        new(input, new ConnectionRecoveryVisionDetector(),
            maximumAttempts: maximumAttempts, phaseTimeout: phaseTimeout, retryDelay: retryDelay);

    private static void SendStableButton(
        ConnectionRecoveryCoordinator coordinator,
        VisionProfile profile,
        DateTimeOffset start)
    {
        coordinator.ProcessFrame(CreateFrame(start, true), profile);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile);
    }

    private static VisionProfile CreateProfile() => new()
    {
        MinimumBlobArea = 5,
        ReconnectButtonRegion = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
        ReconnectButtonRegionCalibrated = true,
        ReconnectButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
    };

    private static ScreenCaptureFrame CreateFrame(DateTimeOffset timestamp, bool showButton)
    {
        const int width = 100;
        const int height = 80;
        var pixels = new byte[width * height * 4];
        if (showButton)
        {
            for (var y = 20; y < 40; y++)
            for (var x = 60; x < 80; x++)
            {
                var index = (y * width + x) * 4;
                pixels[index + 2] = 255;
                pixels[index + 3] = 255;
            }
        }

        return new ScreenCaptureFrame(width, height, width * 4, pixels, timestamp, "Roblox", 1234);
    }

    private sealed class FakeInput : IRobloxInput
    {
        public bool IsArmed { get; private set; }
        public int Clicks { get; private set; }
        public void Arm() => IsArmed = true;
        public void SetLeftButton(bool isDown) { }
        public void SetKey(ushort virtualKey, bool isDown) { }
        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame)
        {
            if (!IsArmed) throw new InvalidOperationException("Disarmed.");
            Clicks++;
        }
        public void Disarm() => IsArmed = false;
    }
}
