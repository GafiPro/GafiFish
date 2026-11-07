using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class RobloxInputServiceEmergencyStopTests
{
    [Fact]
    public void EmergencyStop_ReleasesHeldControlsAndRequiresExplicitRearm()
    {
        var sink = new RecordingInputSink();
        using var input = new RobloxInputService(new FakeRobloxWindowCapture(), sink);

        input.Arm();
        input.SetLeftButton(true);
        input.SetKey(0x41, true);

        input.EmergencyStop();

        Assert.False(input.IsArmed);
        Assert.False(input.IsLeftButtonDown);
        Assert.True(sink.Events.Contains(("left", (ushort)0, false)));
        Assert.True(sink.Events.Contains(("key", (ushort)0x41, false)));
        Assert.Throws<InvalidOperationException>(() => input.SetLeftButton(true));
        Assert.Throws<InvalidOperationException>(() => input.SetKey(0x41, true));

        // An explicit new Arm call is the only operation that clears the latch.
        input.Arm();
        input.SetLeftButton(true);
        Assert.True(input.IsArmed);
        Assert.True(input.IsLeftButtonDown);
    }

    private sealed class FakeRobloxWindowCapture : IRobloxWindowCapture
    {
        private const int ProcessId = 12345;

        public ScreenCaptureFrame CaptureForegroundClient() =>
            throw new NotSupportedException("This test does not capture the screen.");

        public bool IsRobloxForeground() => true;

        public bool IsRobloxForeground(int expectedProcessId) =>
            expectedProcessId == ProcessId;

        public int? GetForegroundRobloxProcessId() => ProcessId;
    }

    private sealed class RecordingInputSink : IWindowsInputSink
    {
        public List<(string Kind, ushort Key, bool IsDown)> Events { get; } = [];

        public void SetLeftButton(bool isDown) =>
            Events.Add(("left", 0, isDown));

        public void MoveCursor(int screenX, int screenY)
        {
        }

        public void SetKey(ushort virtualKey, bool isDown) =>
            Events.Add(("key", virtualKey, isDown));
    }
}
