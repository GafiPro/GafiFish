using GafiFish.App.Automation;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class HotbarItemCoordinatorTests
{
    [Fact]
    public void UseItem_SelectsClicksAndRestoresRodSlot()
    {
        var input = new RecordingInput();
        var coordinator = new HotbarItemCoordinator(input);

        coordinator.UseItem(itemSlot: 4, rodSlot: 1);

        Assert.Equal(
            new[]
            {
                "key:52:down",
                "key:52:up",
                "mouse:down",
                "mouse:up",
                "key:49:down",
                "key:49:up"
            },
            input.Events);
        Assert.True(input.IsArmed);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(10, 1)]
    [InlineData(2, -1)]
    [InlineData(2, 10)]
    [InlineData(2, 2)]
    public void InvalidSlotsAreRejectedBeforeAnyInput(int itemSlot, int rodSlot)
    {
        var input = new RecordingInput();
        var coordinator = new HotbarItemCoordinator(input);

        Assert.ThrowsAny<ArgumentException>(() => coordinator.UseItem(itemSlot, rodSlot));
        Assert.Empty(input.Events);
        Assert.True(input.IsArmed);
    }

    [Fact]
    public void DisarmedInputIsRejectedWithoutTryingToRestoreAnotherSlot()
    {
        var input = new RecordingInput { IsArmed = false };
        var coordinator = new HotbarItemCoordinator(input);

        Assert.Throws<InvalidOperationException>(() => coordinator.UseItem(4, 1));

        Assert.Empty(input.Events);
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void ItemSelectionFailureDisarmsAndNeverClicksOrRestoresRod()
    {
        var input = new RecordingInput { FailOnKeyDown = 0x34 };
        var coordinator = new HotbarItemCoordinator(input);

        Assert.Throws<InvalidOperationException>(() => coordinator.UseItem(4, 1));

        Assert.Equal(new[] { "key:52:down", "key:52:up", "disarm" }, input.Events);
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void ClickFailureDisarmsAndDoesNotSendRodKeyAfterFocusLoss()
    {
        var input = new RecordingInput { FailOnMouseDown = true };
        var coordinator = new HotbarItemCoordinator(input);

        Assert.Throws<InvalidOperationException>(() => coordinator.UseItem(4, 1));

        Assert.Equal(
            new[] { "key:52:down", "key:52:up", "mouse:down", "disarm" },
            input.Events);
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void MouseReleaseFailureDisarmsInsteadOfContinuingToRodSlot()
    {
        var input = new RecordingInput { FailOnMouseUp = true };
        var coordinator = new HotbarItemCoordinator(input);

        Assert.Throws<InvalidOperationException>(() => coordinator.UseItem(4, 1));

        Assert.Equal(
            new[] { "key:52:down", "key:52:up", "mouse:down", "mouse:up", "disarm" },
            input.Events);
        Assert.False(input.IsArmed);
    }

    [Fact]
    public void RodRestoreFailureDisarmsAndReleasesTheRodKey()
    {
        var input = new RecordingInput { FailOnKeyDown = 0x31 };
        var coordinator = new HotbarItemCoordinator(input);

        Assert.Throws<InvalidOperationException>(() => coordinator.UseItem(4, 1));

        Assert.Equal(
            new[]
            {
                "key:52:down",
                "key:52:up",
                "mouse:down",
                "mouse:up",
                "key:49:down",
                "key:49:up",
                "disarm"
            },
            input.Events);
        Assert.False(input.IsArmed);
    }

    private sealed class RecordingInput : IRobloxInput
    {
        public List<string> Events { get; } = [];
        public bool IsArmed { get; set; } = true;
        public ushort? FailOnKeyDown { get; init; }
        public bool FailOnMouseDown { get; init; }
        public bool FailOnMouseUp { get; init; }

        public void Arm() => IsArmed = true;

        public void SetLeftButton(bool isDown)
        {
            Events.Add(isDown ? "mouse:down" : "mouse:up");
            if ((isDown && FailOnMouseDown) || (!isDown && FailOnMouseUp))
                throw new InvalidOperationException("Simulated mouse input failure.");
        }

        public void SetKey(ushort virtualKey, bool isDown)
        {
            Events.Add($"key:{virtualKey}:{(isDown ? "down" : "up")}");
            if (isDown && virtualKey == FailOnKeyDown)
                throw new InvalidOperationException("Simulated key input failure.");
        }

        public void ClickClient(int x, int y, ScreenCaptureFrame frame) =>
            throw new NotSupportedException();

        public void Disarm()
        {
            Events.Add("disarm");
            IsArmed = false;
        }
    }
}
