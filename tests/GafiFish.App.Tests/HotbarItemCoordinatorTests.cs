using GafiFish.App.Automation;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class HotbarItemCoordinatorTests
{
    [Fact]
    public void UseItemSelectsClicksAndRestoresRod()
    {
        var input = new RecordingInput();
        new HotbarItemCoordinator(input).UseItem(5, 1);
        Assert.Equal(new[] { "key:53:down", "key:53:up", "mouse:down", "mouse:up", "key:49:down", "key:49:up" }, input.Events);
    }

    [Fact]
    public void SameSlotIsRejected()
    {
        var input = new RecordingInput();
        Assert.Throws<ArgumentException>(() => new HotbarItemCoordinator(input).UseItem(1, 1));
        Assert.Empty(input.Events);
    }

    [Fact]
    public void RodIsRestoredWhenClickFails()
    {
        var input = new RecordingInput { FailMouseDown = true };
        Assert.Throws<InvalidOperationException>(() => new HotbarItemCoordinator(input).UseItem(5, 1));
        Assert.Contains("key:49:down", input.Events);
        Assert.Contains("key:49:up", input.Events);
    }

    private sealed class RecordingInput : IRobloxInput
    {
        public List<string> Events { get; } = [];
        public bool IsArmed { get; private set; }
        public bool FailMouseDown { get; init; }
        public void Arm() => IsArmed = true;
        public void SetLeftButton(bool down)
        {
            Events.Add(down ? "mouse:down" : "mouse:up");
            if (down && FailMouseDown) throw new InvalidOperationException("Simulated failure.");
        }
        public void SetKey(ushort key, bool down) => Events.Add($"key:{key}:{(down ? "down" : "up")}");
        public void ClickClient(int x, int y, ScreenCaptureFrame frame) => throw new NotSupportedException();
        public void Disarm() => IsArmed = false;
    }
}
