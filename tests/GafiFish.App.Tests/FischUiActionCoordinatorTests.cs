using GafiFish.App.Automation;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class FischUiActionCoordinatorTests
{
    [Fact]
    public void SendsExactlyOneClickAfterThreeDistinctStableFramesThenDisarms()
    {
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = new FischUiActionCoordinator(input, new FischUiActionVisionDetector());
        var profile = CreateProfile();
        coordinator.Start(FischUiActionKind.AppraiseFish, profile, start);

        coordinator.ProcessFrame(CreateFrame(start, true), profile, start);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile, start.AddMilliseconds(120));
        Assert.Equal(0, input.Clicks);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile, start.AddMilliseconds(240));

        Assert.Equal(1, input.Clicks);
        Assert.False(input.IsArmed);
        Assert.False(coordinator.IsStarted);
        Assert.True(coordinator.HasCompleted);
        Assert.Contains("no retry", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(360), true), profile, start.AddMilliseconds(360));
        Assert.Equal(1, input.Clicks);
    }

    [Fact]
    public void DuplicateFrameCannotSatisfyStabilityThreshold()
    {
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = new FischUiActionCoordinator(input, new FischUiActionVisionDetector());
        var profile = CreateProfile();
        coordinator.Start(FischUiActionKind.EnchantRod, profile, start);
        var frame = CreateFrame(start, true);

        coordinator.ProcessFrame(frame, profile, start);
        coordinator.ProcessFrame(frame, profile, start.AddMilliseconds(120));
        Assert.Equal(0, input.Clicks);
        Assert.Equal(1, coordinator.StableFrames);

        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile, start.AddMilliseconds(240));
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(360), true), profile, start.AddMilliseconds(360));
        Assert.Equal(1, input.Clicks);
    }

    [Fact]
    public void DoesNotClickWhenButtonIsNotCalibrated()
    {
        var profile = new VisionProfile();
        var input = new FakeInput();
        var coordinator = new FischUiActionCoordinator(input, new FischUiActionVisionDetector());
        Assert.Throws<InvalidOperationException>(() => coordinator.Start(FischUiActionKind.AppraiseFish, profile));
        Assert.Empty(input.Events);
        Assert.False(coordinator.IsStarted);
    }

    [Fact]
    public void TimesOutWithoutRetryOrInput()
    {
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeInput();
        var coordinator = new FischUiActionCoordinator(input, new FischUiActionVisionDetector(), maximumWait: TimeSpan.FromSeconds(1));
        var profile = CreateProfile();
        coordinator.Start(FischUiActionKind.TreasureBoxRow2, profile, start);
        coordinator.ProcessFrame(CreateFrame(start.AddSeconds(2), false), profile, start.AddSeconds(2));
        Assert.False(coordinator.IsStarted);
        Assert.False(input.IsArmed);
        Assert.Empty(input.Events);
        Assert.Contains("timeout", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OutOfFrameCoordinatesAreNeverInvented()
    {
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeInput { ThrowOnClick = true };
        var coordinator = new FischUiActionCoordinator(input, new FischUiActionVisionDetector());
        var profile = CreateProfile();
        coordinator.Start(FischUiActionKind.TreasureTakeNow, profile, start);
        coordinator.ProcessFrame(CreateFrame(start, true), profile, start);
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(120), true), profile, start.AddMilliseconds(120));
        coordinator.ProcessFrame(CreateFrame(start.AddMilliseconds(240), true), profile, start.AddMilliseconds(240));
        Assert.False(coordinator.IsStarted);
        Assert.False(input.IsArmed);
        Assert.False(coordinator.HasCompleted);
        Assert.Contains("stopped safely", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static VisionProfile CreateProfile() => new()
    {
        FischUiActions = new Dictionary<FischUiActionKind, FischUiActionCalibration>
        {
            [FischUiActionKind.AppraiseFish] = new()
            {
                Region = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
                RegionCalibrated = true,
                ButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
            },
            [FischUiActionKind.TreasureBoxRow2] = new()
            {
                Region = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
                RegionCalibrated = true,
                ButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
            },
            [FischUiActionKind.EnchantRod] = new()
            {
                Region = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
                RegionCalibrated = true,
                ButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
            },
            [FischUiActionKind.TreasureTakeNow] = new()
            {
                Region = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
                RegionCalibrated = true,
                ButtonColor = HsvColorSampler.FromBgra(0, 0, 255)
            }
        },
        MinimumBlobArea = 5
    };

    private static ScreenCaptureFrame CreateFrame(DateTimeOffset at, bool showButton)
    {
        const int width = 100, height = 80;
        var pixels = new byte[width * height * 4];
        if (showButton)
        {
            for (var y = 20; y < 32; y++)
                for (var x = 60; x < 74; x++)
                {
                    var index = (y * width + x) * 4;
                    pixels[index] = 0; pixels[index + 1] = 0; pixels[index + 2] = 255; pixels[index + 3] = 255;
                }
        }
        return new ScreenCaptureFrame(width, height, width * 4, pixels, at, "Roblox", 1234);
    }

    private sealed class FakeInput : IRobloxInput
    {
        public bool IsArmed { get; private set; }
        public int Clicks { get; private set; }
        public bool ThrowOnClick { get; init; }
        public List<string> Events { get; } = [];
        public void Arm() => IsArmed = true;
        public void SetLeftButton(bool isDown) => Events.Add(isDown ? "down" : "up");
        public void SetKey(ushort virtualKey, bool isDown) => Events.Add($"key:{virtualKey}:{isDown}");
        public void MoveClient(int clientX, int clientY, ScreenCaptureFrame frame) => Events.Add("move");
        public (int X, int Y)? GetCursorClientPosition(ScreenCaptureFrame frame) => (0, 0);
        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame)
        {
            Events.Add("click");
            if (ThrowOnClick) throw new InvalidOperationException("fake click refused");
            Clicks++;
        }
        public void Disarm() => IsArmed = false;
        public void ReleaseAll() { }
    }
}
