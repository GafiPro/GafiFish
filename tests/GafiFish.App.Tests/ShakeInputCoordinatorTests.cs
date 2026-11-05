using GafiFish.App.Automation;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using OpenCvSharp;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class ShakeInputCoordinatorTests
{
    [Fact]
    public void ProcessFrame_ClicksOnlyAfterTwoStableFrames()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new ShakeInputCoordinator(input);
        var start = DateTimeOffset.UtcNow.AddSeconds(-2);

        var first = ProcessAt(coordinator,
            Frame(start),
            [Prompt(50, 50)]);

        Assert.Equal(ShakeInputResultKind.Stabilising, first.Kind);
        Assert.Equal(0, input.Clicks);

        var second = ProcessAt(coordinator,
            Frame(start.AddMilliseconds(100)),
            [Prompt(52, 51)]);

        Assert.Equal(ShakeInputResultKind.Clicked, second.Kind);
        Assert.Equal(1, input.Clicks);
        Assert.Equal((52, 51), input.LastClick);
    }

    [Fact]
    public void ProcessFrame_DoesNotClickTheSameStaticPromptTwice()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new ShakeInputCoordinator(input);
        var start = DateTimeOffset.UtcNow.AddSeconds(-2);

        _ = ProcessAt(coordinator, Frame(start), [Prompt(50, 50)]);
        _ = ProcessAt(coordinator, Frame(start.AddMilliseconds(100)), [Prompt(50, 50)]);

        var repeated = ProcessAt(coordinator,
            Frame(start.AddMilliseconds(650)),
            [Prompt(51, 50)]);

        Assert.Equal(ShakeInputResultKind.WaitingForPromptChange, repeated.Kind);
        Assert.Equal(1, input.Clicks);
    }

    [Fact]
    public void ProcessFrame_ClicksAgainOnlyAfterPromptMovesAndStabilises()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new ShakeInputCoordinator(input);
        var start = DateTimeOffset.UtcNow.AddSeconds(-3);

        _ = ProcessAt(coordinator, Frame(start), [Prompt(20, 30)]);
        _ = ProcessAt(coordinator, Frame(start.AddMilliseconds(100)), [Prompt(20, 30)]);
        Assert.Equal(1, input.Clicks);

        var movedFirst = ProcessAt(coordinator,
            Frame(start.AddMilliseconds(650)),
            [Prompt(65, 30)]);
        Assert.Equal(ShakeInputResultKind.Stabilising, movedFirst.Kind);

        var movedSecond = ProcessAt(coordinator,
            Frame(start.AddMilliseconds(750)),
            [Prompt(66, 31)]);

        Assert.Equal(ShakeInputResultKind.Clicked, movedSecond.Kind);
        Assert.Equal(2, input.Clicks);
        Assert.Equal((66, 31), input.LastClick);
    }

    [Fact]
    public void ProcessFrame_RequiresPromptToDisappearBeforeSamePositionCanBeClickedAgain()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new ShakeInputCoordinator(input, new ShakeInputSettings
        {
            MaximumFrameAge = TimeSpan.FromSeconds(10)
        });
        var start = DateTimeOffset.UtcNow.AddSeconds(-3);

        _ = ProcessAt(coordinator, Frame(start), [Prompt(30, 30)]);
        _ = ProcessAt(coordinator, Frame(start.AddMilliseconds(100)), [Prompt(30, 30)]);

        _ = ProcessAt(coordinator, Frame(start.AddMilliseconds(600)), []);
        _ = ProcessAt(coordinator, Frame(start.AddMilliseconds(700)), []);

        var firstAfterReturn = ProcessAt(coordinator,
            Frame(start.AddMilliseconds(800)),
            [Prompt(30, 30)]);
        Assert.Equal(ShakeInputResultKind.Stabilising, firstAfterReturn.Kind);

        var secondAfterReturn = ProcessAt(coordinator,
            Frame(start.AddMilliseconds(900)),
            [Prompt(31, 30)]);

        Assert.Equal(ShakeInputResultKind.Clicked, secondAfterReturn.Kind);
        Assert.Equal(2, input.Clicks);
    }

    [Fact]
    public void ProcessFrame_RejectsStaleFrameWithoutClicking()
    {
        var input = new FakeRobloxInput();
        input.Arm();
        var coordinator = new ShakeInputCoordinator(input);

        var result = coordinator.ProcessFrame(
            Frame(DateTimeOffset.UtcNow.AddSeconds(-2)),
            [Prompt(50, 50)]);

        Assert.Equal(ShakeInputResultKind.StaleFrame, result.Kind);
        Assert.Equal(0, input.Clicks);
    }

    private static ShakeInputResult ProcessAt(
        ShakeInputCoordinator coordinator,
        ScreenCaptureFrame frame,
        IReadOnlyList<DetectedMarker> prompts) =>
        coordinator.ProcessFrame(frame, prompts, frame.CapturedAt);

    private static DetectedMarker Prompt(double x, double y) =>
        new(x, y, new Rect((int)x - 5, (int)y - 5, 10, 10), 60, 0.95);

    private static ScreenCaptureFrame Frame(DateTimeOffset timestamp) =>
        new(100, 100, 400, new byte[40_000], timestamp, "Roblox", 1234);

    private sealed class FakeRobloxInput : IRobloxInput
    {
        public bool IsArmed { get; private set; }
        public int Clicks { get; private set; }
        public (int X, int Y)? LastClick { get; private set; }

        public void Arm() => IsArmed = true;

        public void SetLeftButton(bool isDown) => EnsureArmed();

        public void SetKey(ushort virtualKey, bool isDown) => EnsureArmed();

        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame)
        {
            EnsureArmed();
            Clicks++;
            LastClick = (clientX, clientY);
        }

        public void Disarm() => IsArmed = false;

        private void EnsureArmed()
        {
            if (!IsArmed)
            {
                throw new InvalidOperationException("Fake input is disarmed.");
            }
        }
    }
}
