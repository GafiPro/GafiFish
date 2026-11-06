using GafiFish.App.Automation;
using GafiFish.Core.Aquarium;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class AquariumAutomationCoordinatorTests
{
    [Fact]
    public void ClicksOnceAfterStableDetectionAndRecordsOnlyAfterButtonDisappears()
    {
        var start = new DateTimeOffset(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var confirmed = new List<(AquariumActionKind Kind, DateTimeOffset At)>();
        var coordinator = new AquariumAutomationCoordinator(
            input, new AquariumButtonVisionDetector(),
            (kind, at) => confirmed.Add((kind, at)));
        var profile = CreateProfile();
        var schedule = new AquariumScheduleState
        {
            GuppiesBest = new AquariumFoodProgress(LastFedAt: start.AddHours(-1))
        };

        coordinator.Start();
        coordinator.ProcessFrame(CreateFrame(start, true), profile, schedule,
            new AquariumAutomationOptions(true, false, false));
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile, schedule,
            new AquariumAutomationOptions(true, false, false));
        Assert.Equal(0, input.Clicks);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile, schedule,
            new AquariumAutomationOptions(true, false, false));
        Assert.Equal(1, input.Clicks);
        Assert.Empty(confirmed);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(360), false), profile, schedule,
            new AquariumAutomationOptions(true, false, false));
        Assert.Empty(confirmed);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(480), false), profile, schedule,
            new AquariumAutomationOptions(true, false, false));

        Assert.Single(confirmed);
        Assert.Equal(AquariumActionKind.FeedGuppiesBest, confirmed[0].Kind);
        Assert.Equal(start.AddMilliseconds(480), confirmed[0].At);
        Assert.Contains("Confirmed", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
        coordinator.Stop();
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void DoesNotClickBeforeCooldownIsReady()
    {
        var start = new DateTimeOffset(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = new AquariumAutomationCoordinator(
            input, new AquariumButtonVisionDetector(), (_, _) => { });
        var schedule = new AquariumScheduleState
        {
            GuppiesBest = new AquariumFoodProgress(LastFedAt: start.AddMinutes(-59))
        };

        coordinator.Start();
        coordinator.ProcessFrame(CreateFrame(start, true), CreateProfile(), schedule,
            new AquariumAutomationOptions(true, false, false));

        Assert.Equal(0, input.Clicks);
        Assert.Contains("waiting", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FailsClosedWithoutRetryWhenButtonDoesNotDisappear()
    {
        var start = new DateTimeOffset(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = new AquariumAutomationCoordinator(
            input, new AquariumButtonVisionDetector(), (_, _) => { },
            phaseTimeout: TimeSpan.FromSeconds(1));
        var schedule = new AquariumScheduleState
        {
            GuppiesBest = new AquariumFoodProgress(LastFedAt: start.AddHours(-1))
        };

        coordinator.Start();
        coordinator.ProcessFrame(CreateFrame(start, true), CreateProfile(), schedule, new(true, false, false));
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), CreateProfile(), schedule, new(true, false, false));
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), CreateProfile(), schedule, new(true, false, false));
        coordinator.ProcessFrame(CreateFrame(start.AddSeconds(2), true), CreateProfile(), schedule, new(true, false, false));

        Assert.False(coordinator.IsStarted);
        Assert.False(input.IsArmed);
        Assert.Equal(1, input.Clicks);
        Assert.Contains("No retry", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestockAndClaimAreDisabledUnlessExplicitlyEnabled()
    {
        var start = new DateTimeOffset(2026, 11, 6, 13, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = new AquariumAutomationCoordinator(
            input, new AquariumButtonVisionDetector(), (_, _) => { });
        var schedule = new AquariumScheduleState
        {
            GuppiesBest = new AquariumFoodProgress(LastFedAt: start.AddHours(-1), LastRestockedAt: start.AddHours(-1)),
            LastClaimedAt = start.AddHours(-1)
        };
        var feedOnlyProfile = CreateProfile(AquariumActionKind.FeedGuppiesBest);
        coordinator.Start();

        for (var i = 0; i < 3; i++)
            coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(i * 120), true), feedOnlyProfile, schedule,
                new AquariumAutomationOptions(true, false, false));

        Assert.Equal(1, input.Clicks);
        Assert.Equal(AquariumActionKind.FeedGuppiesBest, coordinator.PendingAction);
    }

    private static VisionProfile CreateProfile(AquariumActionKind action = AquariumActionKind.FeedGuppiesBest) => new()
    {
        MinimumBlobArea = 5,
        AquariumButtons = new Dictionary<AquariumActionKind, AquariumButtonCalibration>
        {
            [action] = new()
            {
                Region = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
                RegionCalibrated = true,
                ButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
            }
        }
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
