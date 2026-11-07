using GafiFish.App.Automation;
using GafiFish.Core.Automation;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class AnglerQuestAutomationCoordinatorTests
{
    [Fact]
    public void AcceptRequest_ClicksOnceAfterStableDetectionAndConfirmsDisappearance()
    {
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var tracker = new AnglerQuestTracker();
        var coordinator = new AnglerQuestAutomationCoordinator(
            input, new AnglerQuestButtonVisionDetector(),
            (kind, at) =>
            {
                Assert.Equal(AnglerQuestButtonKind.AcceptRequest, kind);
                tracker.StartTarget("Ancient Kraken", at);
            });
        var profile = CreateProfile();
        coordinator.Start();

        coordinator.ProcessFrame(CreateFrame(start, true), profile, tracker, "Ancient Kraken");
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile, tracker, "Ancient Kraken");
        Assert.Equal(0, input.Clicks);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile, tracker, "Ancient Kraken");
        Assert.Equal(1, input.Clicks);
        Assert.Equal(AnglerQuestButtonKind.AcceptRequest, coordinator.PendingKind);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(360), false), profile, tracker, "Ancient Kraken");
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(480), false), profile, tracker, "Ancient Kraken");

        Assert.True(tracker.IsTracking);
        Assert.Equal("Ancient Kraken", tracker.TargetSpecies);
        Assert.False(coordinator.IsStarted);
        Assert.False(input.IsArmed);
        Assert.Equal(1, input.Clicks);
        Assert.Contains("acceptance visually confirmed", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubmitFish_StartsCooldownOnlyAfterButtonDisappears()
    {
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var tracker = new AnglerQuestTracker();
        tracker.StartTarget("Ancient Kraken", start);
        Assert.True(tracker.MarkTargetFishCaught("Ancient Kraken", start.AddMinutes(1)));

        var coordinator = new AnglerQuestAutomationCoordinator(
            input, new AnglerQuestButtonVisionDetector(),
            (kind, at) =>
            {
                Assert.Equal(AnglerQuestButtonKind.SubmitFish, kind);
                tracker.ConfirmQuestSubmitted(at);
            });
        var profile = CreateProfile();
        coordinator.Start();

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(1), true), profile, tracker, "Ancient Kraken");
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(1).AddMilliseconds(120), true), profile, tracker, "Ancient Kraken");
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(1).AddMilliseconds(240), true), profile, tracker, "Ancient Kraken");
        Assert.Equal(1, input.Clicks);
        Assert.False(tracker.IsInCooldown);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(1).AddMilliseconds(360), false), profile, tracker, "Ancient Kraken");
        Assert.False(tracker.IsInCooldown);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(1).AddMilliseconds(480), false), profile, tracker, "Ancient Kraken");

        Assert.True(tracker.IsInCooldown);
        Assert.Equal(start.AddMinutes(3).AddMilliseconds(480), tracker.NextRequestAvailableAt);
        Assert.False(coordinator.IsStarted);
        Assert.False(input.IsArmed);
        Assert.Equal(1, input.Clicks);
    }

    [Fact]
    public void DuplicateFrameCannotSatisfyStableDetectionThreshold()
    {
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var tracker = new AnglerQuestTracker();
        var coordinator = new AnglerQuestAutomationCoordinator(
            input, new AnglerQuestButtonVisionDetector(), (_, _) => { });
        var profile = CreateProfile();
        var optionsFrame = CreateFrame(start, true);

        coordinator.Start();
        coordinator.ProcessFrame(optionsFrame, profile, tracker, "Ancient Kraken");
        coordinator.ProcessFrame(optionsFrame, profile, tracker, "Ancient Kraken");

        Assert.Equal(0, input.Clicks);
        Assert.Contains("duplicate or out-of-order", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile, tracker, "Ancient Kraken");
        Assert.Equal(0, input.Clicks);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile, tracker, "Ancient Kraken");
        Assert.Equal(1, input.Clicks);
        coordinator.Stop();
    }

    [Fact]
    public void DoesNotRetryWhenButtonFailsToDisappear()
    {
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var tracker = new AnglerQuestTracker();
        var coordinator = new AnglerQuestAutomationCoordinator(
            input, new AnglerQuestButtonVisionDetector(), (_, _) => { },
            phaseTimeout: TimeSpan.FromSeconds(1));
        var profile = CreateProfile();
        coordinator.Start();

        coordinator.ProcessFrame(CreateFrame(start, true), profile, tracker, "Ancient Kraken");
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile, tracker, "Ancient Kraken");
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile, tracker, "Ancient Kraken");
        coordinator.ProcessFrame(CreateFrame(start.AddSeconds(2), true), profile, tracker, "Ancient Kraken");

        Assert.False(coordinator.IsStarted);
        Assert.False(input.IsArmed);
        Assert.Equal(1, input.Clicks);
        Assert.Contains("No retry", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static VisionProfile CreateProfile()
    {
        var calibration = new AnglerQuestButtonCalibration
        {
            Region = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
            RegionCalibrated = true,
            ButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
        };

        return new VisionProfile
        {
            MinimumBlobArea = 5,
            AnglerQuestButtons = new Dictionary<AnglerQuestButtonKind, AnglerQuestButtonCalibration>
            {
                [AnglerQuestButtonKind.AcceptRequest] = calibration,
                [AnglerQuestButtonKind.SubmitFish] = calibration
            }
        };
    }

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
            if (!IsArmed)
                throw new InvalidOperationException("Input is disarmed.");
            Clicks++;
        }

        public void Disarm() => IsArmed = false;
    }
}
