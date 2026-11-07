using GafiFish.App.Automation;
using GafiFish.Core.Control;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class RhythmNoteInputCoordinatorTests
{
    [Fact]
    public void ProcessFrame_TapsTheMatchingLaneWhenPredictedNoteReachesHitLine()
    {
        var input = new RecordingInput();
        var coordinator = new RhythmNoteInputCoordinator(input);
        var profile = CreateProfile(leadMilliseconds: 160);
        var origin = DateTimeOffset.UtcNow;

        coordinator.Start(profile);
        coordinator.ProcessFrame(CreateFrame(195, origin), profile, allowInput: false);
        coordinator.ProcessFrame(CreateFrame(205, origin.AddMilliseconds(100)), profile, allowInput: false);
        coordinator.ProcessFrame(CreateFrame(215, origin.AddMilliseconds(200)), profile, allowInput: true);

        Assert.Equal(
            new[] { (0x44, true), (0x44, false) },
            input.KeyEvents);
        Assert.Equal(1, coordinator.HitCount);
        Assert.Contains("hit 1 lane", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessFrame_IgnoresRepeatedFramesWithoutSendingAnotherKey()
    {
        var input = new RecordingInput();
        var coordinator = new RhythmNoteInputCoordinator(input);
        var profile = CreateProfile(leadMilliseconds: 160);
        var origin = DateTimeOffset.UtcNow;
        var frame1 = CreateFrame(195, origin);
        var frame2 = CreateFrame(205, origin.AddMilliseconds(100));
        var frame3 = CreateFrame(215, origin.AddMilliseconds(200));

        coordinator.Start(profile);
        coordinator.ProcessFrame(frame1, profile, allowInput: true);
        coordinator.ProcessFrame(frame2, profile, allowInput: true);
        coordinator.ProcessFrame(frame3, profile, allowInput: true);
        coordinator.ProcessFrame(frame3, profile, allowInput: true);

        Assert.Equal(2, input.KeyEvents.Count);
        Assert.Equal(1, coordinator.HitCount);
    }

    [Fact]
    public void ProcessFrame_DoesNotSendLateKeyTapAfterNotePassedHitWindow()
    {
        var input = new RecordingInput();
        var coordinator = new RhythmNoteInputCoordinator(input);
        var profile = CreateProfile(leadMilliseconds: 160);
        var origin = DateTimeOffset.UtcNow;

        coordinator.Start(profile);
        coordinator.ProcessFrame(CreateFrame(195, origin), profile, allowInput: true);
        coordinator.ProcessFrame(CreateFrame(220, origin.AddMilliseconds(100)), profile, allowInput: true);

        Assert.Empty(input.KeyEvents);
        Assert.Equal(0, coordinator.HitCount);
    }

    [Fact]
    public void Start_RefusesMissingRhythmCalibration()
    {
        var input = new RecordingInput();
        var coordinator = new RhythmNoteInputCoordinator(input);

        Assert.Throws<InvalidOperationException>(() =>
            coordinator.Start(new VisionProfile { RodMinigameMode = RodMinigameMode.TranquilityRhythm }));
        Assert.Empty(input.KeyEvents);
        Assert.False(coordinator.IsStarted);
    }

    [Fact]
    public void Stop_DisarmsInputAndClearsTracking()
    {
        var input = new RecordingInput();
        var coordinator = new RhythmNoteInputCoordinator(input);
        var profile = CreateProfile(leadMilliseconds: 160);

        coordinator.Start(profile);
        coordinator.Stop();

        Assert.False(input.IsArmed);
        Assert.False(coordinator.IsStarted);
        Assert.Equal("Tranquility Rhythm stopped; all held inputs released.", coordinator.LastStatus);
    }

    private static VisionProfile CreateProfile(double leadMilliseconds) => new()
    {
        RodMinigameMode = RodMinigameMode.TranquilityRhythm,
        FishingRegion = new NormalizedRegion(0.1, 0.1, 0.8, 0.8),
        FishingRegionCalibrated = true,
        RhythmLaneColors =
        [
            HsvColorSampler.FromBgra(0, 0, 255, 3, 30, 30),
            HsvColorSampler.FromBgra(0, 255, 0, 3, 30, 30),
            HsvColorSampler.FromBgra(255, 0, 0, 3, 30, 30),
            HsvColorSampler.FromBgra(0, 255, 255, 3, 30, 30)
        ],
        RhythmHitLinePosition = 0.85,
        RhythmHitLineCalibrated = true,
        RhythmInputLeadMilliseconds = leadMilliseconds,
        MinimumBlobArea = 5,
        MinimumConfidence = 0.60
    };

    private static ScreenCaptureFrame CreateFrame(int noteY, DateTimeOffset capturedAt)
    {
        const int width = 400;
        const int height = 300;
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
            pixels[index + 3] = 255;

        const int centerX = 80;
        const int radius = 7;
        for (var y = noteY - radius; y <= noteY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                continue;
            var dx = x - centerX;
            var dy = y - noteY;
            if ((dx * dx) + (dy * dy) > radius * radius)
                continue;

            var index = (y * width + x) * 4;
            pixels[index] = 0;
            pixels[index + 1] = 0;
            pixels[index + 2] = 255;
        }

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels,
            capturedAt, "Roblox", 1234);
    }

    private sealed class RecordingInput : IRobloxInput
    {
        public List<(int Key, bool IsDown)> KeyEvents { get; } = [];
        public bool IsArmed { get; private set; }

        public void Arm() => IsArmed = true;
        public void Disarm() => IsArmed = false;
        public void SetLeftButton(bool isDown) { }
        public void SetKey(ushort virtualKey, bool isDown)
        {
            if (!IsArmed)
                throw new InvalidOperationException("Input is not armed.");
            KeyEvents.Add((virtualKey, isDown));
        }
        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame) { }
    }
}
