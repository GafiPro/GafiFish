using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class RobloxInputServiceGuardTests
{
    [Fact]
    public void Arm_RefusesWhenAnotherApplicationIsForeground()
    {
        var capture = new FakeRobloxWindowCapture { ForegroundRobloxProcessId = null };
        var sink = new RecordingWindowsInputSink();
        using var input = new RobloxInputService(capture, sink);

        Assert.Throws<InvalidOperationException>(() => input.Arm());
        Assert.False(input.IsArmed);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void PressesAreRejectedAndHeldMouseIsReleasedWhenFocusLeavesRoblox()
    {
        var capture = new FakeRobloxWindowCapture { ForegroundRobloxProcessId = 120 };
        var sink = new RecordingWindowsInputSink();
        using var input = new RobloxInputService(capture, sink);

        input.Arm();
        input.SetLeftButton(true);
        capture.ForegroundRobloxProcessId = null;

        var error = Assert.Throws<InvalidOperationException>(() => input.SetKey(0x31, true));

        Assert.Contains("not the foreground application", error.Message);
        Assert.False(input.IsArmed);
        Assert.False(input.IsLeftButtonDown);
        Assert.Equal(new[] { "mouse-down", "mouse-up" }, sink.Events);
    }

    [Fact]
    public void SwitchingToAnotherRobloxProcessAlsoRejectsFurtherInput()
    {
        var capture = new FakeRobloxWindowCapture { ForegroundRobloxProcessId = 120 };
        var sink = new RecordingWindowsInputSink();
        using var input = new RobloxInputService(capture, sink);

        input.Arm();
        capture.ForegroundRobloxProcessId = 121;

        Assert.Throws<InvalidOperationException>(() => input.SetKey(0x31, true));
        Assert.False(input.IsArmed);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void CursorMoveIsRejectedForAFrameFromAnotherRobloxProcess()
    {
        var capture = new FakeRobloxWindowCapture { ForegroundRobloxProcessId = 120 };
        var sink = new RecordingWindowsInputSink();
        using var input = new RobloxInputService(capture, sink);

        input.Arm();

        Assert.Throws<InvalidOperationException>(() => input.MoveClient(15, 20, Frame(processId: 121)));
        Assert.False(input.IsArmed);
        Assert.Empty(sink.Events);
    }

    [Fact]
    public void ClickAndCursorMovementAreRejectedWhenAnotherApplicationHasFocus()
    {
        var capture = new FakeRobloxWindowCapture { ForegroundRobloxProcessId = 120 };
        var sink = new RecordingWindowsInputSink();
        using var input = new RobloxInputService(capture, sink);

        input.Arm();
        capture.ForegroundRobloxProcessId = null;

        Assert.Throws<InvalidOperationException>(() => input.ClickClient(15, 20, Frame(processId: 120)));
        Assert.False(input.IsArmed);
        Assert.Empty(sink.Events);
    }

    private static ScreenCaptureFrame Frame(int processId) =>
        new(100, 80, 400, new byte[100 * 80 * 4], DateTimeOffset.UtcNow,
            "Roblox", processId, 10, 20);

    private sealed class FakeRobloxWindowCapture : IRobloxWindowCapture
    {
        public int? ForegroundRobloxProcessId { get; set; }

        public ScreenCaptureFrame CaptureForegroundClient() =>
            throw new NotSupportedException("Capture is not needed by this guard test.");

        public bool IsRobloxForeground() => ForegroundRobloxProcessId.HasValue;

        public bool IsRobloxForeground(int expectedProcessId) =>
            ForegroundRobloxProcessId == expectedProcessId;

        public int? GetForegroundRobloxProcessId() => ForegroundRobloxProcessId;
    }

    private sealed class RecordingWindowsInputSink : IWindowsInputSink
    {
        public List<string> Events { get; } = [];

        public void SetLeftButton(bool isDown) =>
            Events.Add(isDown ? "mouse-down" : "mouse-up");

        public void MoveCursor(int screenX, int screenY) =>
            Events.Add($"move:{screenX},{screenY}");

        public void SetKey(ushort virtualKey, bool isDown) =>
            Events.Add($"key-{(isDown ? "down" : "up")}:{virtualKey}");
    }
}
