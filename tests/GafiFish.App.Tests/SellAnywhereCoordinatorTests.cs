using GafiFish.App.Automation;
using GafiFish.Core.Automation;
using GafiFish.Core.Scheduling;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class SellAnywhereCoordinatorTests
{
    [Fact]
    public void ProcessFrame_OpensClicksVerifiesAndClosesInventoryOnce()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeRobloxInput();
        var schedule = new PeriodicSellSchedule();
        var machine = CreateCatchDetectedMachine(start);
        var coordinator = new SellAnywhereCoordinator(
            input,
            new SellAnywhereVisionDetector(),
            schedule,
            machine);

        var profile = CreateProfile();
        coordinator.Start(start);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10), fillItems: false, showSellButton: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(120), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(240), fillItems: false), profile, true);
        Assert.Equal(FishingState.SellingInventory, machine.CurrentState);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(360), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(480), fillItems: false), profile, true);
        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);
        Assert.Equal(1, input.ClientClicks);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(1130), fillItems: true), profile, true);
        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(1250), fillItems: true), profile, true);

        Assert.Equal(FishingState.Preparing, machine.CurrentState);
        Assert.False(schedule.SalePending);
        Assert.Equal(start.AddMinutes(20).AddMilliseconds(1250), schedule.NextDueAt);
        Assert.Equal(2, input.InventoryTogglePresses);
        Assert.Equal(1, input.ClientClicks);
        Assert.Contains("visually verified", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);

        coordinator.Stop();
        Assert.False(input.IsArmed);
    }


    [Fact]
    public void ProcessFrame_PreservesInventoryThatWasAlreadyOpen()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeRobloxInput();
        var schedule = new PeriodicSellSchedule();
        var machine = CreateCatchDetectedMachine(start);
        var coordinator = new SellAnywhereCoordinator(
            input,
            new SellAnywhereVisionDetector(),
            schedule,
            machine);

        var profile = CreateProfile();
        coordinator.Start(start);

        // The Sell Anywhere button is already visible on the first due frame.
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(120), fillItems: false), profile, true);
        Assert.Equal(FishingState.SellingInventory, machine.CurrentState);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(240), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(360), fillItems: false), profile, true);
        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);
        Assert.Equal(1, input.ClientClicks);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(1100), fillItems: true), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(1220), fillItems: true), profile, true);

        Assert.Equal(FishingState.Preparing, machine.CurrentState);
        Assert.Equal(0, input.InventoryTogglePresses);
        Assert.Equal(1, input.ClientClicks);
        Assert.False(schedule.SalePending);
        Assert.Contains("visually verified", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessFrame_FailsClosedWhenInventoryDoesNotChange()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeRobloxInput();
        var schedule = new PeriodicSellSchedule();
        var machine = CreateCatchDetectedMachine(start);
        var settings = new SellAnywhereSettings
        {
            VerificationDelay = TimeSpan.Zero,
            PhaseTimeout = TimeSpan.FromSeconds(1)
        };
        var coordinator = new SellAnywhereCoordinator(
            input,
            new SellAnywhereVisionDetector(),
            schedule,
            machine,
            settings);

        var profile = CreateProfile();
        coordinator.Start(start);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10), fillItems: false, showSellButton: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(120), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(240), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(360), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(480), fillItems: false), profile, true);

        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddSeconds(2), fillItems: false), profile, true);

        Assert.Equal(FishingState.Error, machine.CurrentState);
        Assert.False(input.IsArmed);
        Assert.True(schedule.SalePending);
        Assert.Contains("not confirmed", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void ProcessFrame_DoesNotAcceptSingleTransientInventoryChange()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeRobloxInput();
        var schedule = new PeriodicSellSchedule();
        var machine = CreateCatchDetectedMachine(start);
        var settings = new SellAnywhereSettings
        {
            VerificationDelay = TimeSpan.Zero,
            PhaseTimeout = TimeSpan.FromSeconds(1),
            RequiredVerificationFrames = 2
        };
        var coordinator = new SellAnywhereCoordinator(
            input,
            new SellAnywhereVisionDetector(),
            schedule,
            machine,
            settings);

        var profile = CreateProfile();
        coordinator.Start(start);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(120), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(240), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(360), fillItems: false), profile, true);
        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);
        Assert.Equal(1, input.ClientClicks);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(1100), fillItems: true), profile, true);
        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(1220), fillItems: false), profile, true);
        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10).AddMilliseconds(1500), fillItems: false), profile, true);

        Assert.Equal(FishingState.Error, machine.CurrentState);
        Assert.False(input.IsArmed);
        Assert.True(schedule.SalePending);
        Assert.Equal(1, input.ClientClicks);
        Assert.Contains("not confirmed", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessFrame_DuplicateFrameDoesNotSatisfyStableButtonThreshold()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeRobloxInput();
        var schedule = new PeriodicSellSchedule();
        var machine = CreateCatchDetectedMachine(start);
        var coordinator = new SellAnywhereCoordinator(
            input,
            new SellAnywhereVisionDetector(),
            schedule,
            machine);

        coordinator.Start(start);
        var profile = CreateProfile();
        var firstDueFrame = CreateFrame(start.AddMinutes(10), fillItems: false);

        coordinator.ProcessFrame(firstDueFrame, profile, true);
        Assert.Equal(FishingState.OpeningInventory, machine.CurrentState);

        // Passing the exact same capture again must not count as a second
        // independent observation of the Sell button.
        coordinator.ProcessFrame(firstDueFrame, profile, true);
        Assert.Equal(FishingState.OpeningInventory, machine.CurrentState);
        Assert.Equal(0, input.ClientClicks);
        Assert.Contains("duplicate or out-of-order", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);

        coordinator.ProcessFrame(
            CreateFrame(start.AddMinutes(10).AddMilliseconds(120), fillItems: false),
            profile,
            true);
        Assert.Equal(FishingState.SellingInventory, machine.CurrentState);
        Assert.Equal(0, input.ClientClicks);

        coordinator.ProcessFrame(
            CreateFrame(start.AddMinutes(10).AddMilliseconds(240), fillItems: false),
            profile,
            true);
        coordinator.ProcessFrame(
            CreateFrame(start.AddMinutes(10).AddMilliseconds(360), fillItems: false),
            profile,
            true);

        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);
        Assert.Equal(1, input.ClientClicks);
        coordinator.Stop();
    }

    [Fact]
    public void ProcessFrame_OutOfOrderFrameCannotAdvanceSaleWorkflow()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeRobloxInput();
        var schedule = new PeriodicSellSchedule();
        var machine = CreateCatchDetectedMachine(start);
        var coordinator = new SellAnywhereCoordinator(
            input,
            new SellAnywhereVisionDetector(),
            schedule,
            machine);

        coordinator.Start(start);
        var profile = CreateProfile();
        var first = start.AddMinutes(10);

        coordinator.ProcessFrame(CreateFrame(first, fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(first.AddMilliseconds(120), fillItems: false), profile, true);
        Assert.Equal(FishingState.SellingInventory, machine.CurrentState);

        coordinator.ProcessFrame(CreateFrame(first.AddMilliseconds(60), fillItems: false), profile, true);
        Assert.Equal(FishingState.SellingInventory, machine.CurrentState);
        Assert.Equal(0, input.ClientClicks);
        Assert.Contains("duplicate or out-of-order", coordinator.LastStatus, StringComparison.OrdinalIgnoreCase);

        coordinator.ProcessFrame(CreateFrame(first.AddMilliseconds(240), fillItems: false), profile, true);
        coordinator.ProcessFrame(CreateFrame(first.AddMilliseconds(360), fillItems: false), profile, true);
        Assert.Equal(FishingState.VerifyingSale, machine.CurrentState);
        Assert.Equal(1, input.ClientClicks);
        coordinator.Stop();
    }

    [Fact]
    public void ProcessFrame_DoesNotSellDuringActiveFishingCycle()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeRobloxInput();
        var schedule = new PeriodicSellSchedule();
        var machine = CreateCatchDetectedMachine(start);
        var coordinator = new SellAnywhereCoordinator(
            input,
            new SellAnywhereVisionDetector(),
            schedule,
            machine);

        coordinator.Start(start);
        var profile = CreateProfile();

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10), fillItems: false), profile, false);

        Assert.Equal(FishingState.CatchDetected, machine.CurrentState);
        Assert.Equal(0, input.InventoryTogglePresses);
        Assert.Equal(0, input.ClientClicks);
        Assert.True(schedule.SalePending);
    }

    [Fact]
    public void Start_RejectsMissingInventoryVerificationCalibrationWhenSaleBecomesDue()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var input = new FakeRobloxInput();
        var schedule = new PeriodicSellSchedule();
        var machine = CreateCatchDetectedMachine(start);
        var coordinator = new SellAnywhereCoordinator(
            input,
            new SellAnywhereVisionDetector(),
            schedule,
            machine);

        coordinator.Start(start);
        var profile = CreateProfile() with { InventoryItemsRegionCalibrated = false };

        coordinator.ProcessFrame(CreateFrame(start.AddMinutes(10), fillItems: false), profile, true);

        Assert.Equal(FishingState.Error, machine.CurrentState);
        Assert.False(input.IsArmed);
        Assert.Equal(0, input.ClientClicks);
    }

    private static VisionProfile CreateProfile() => new()
    {
        SellButtonRegion = new NormalizedRegion(0.50, 0.10, 0.40, 0.80),
        SellButtonRegionCalibrated = true,
        SellButtonColor = HsvColorSampler.FromBgra(0, 0, 255),
        InventoryItemsRegion = new NormalizedRegion(0.05, 0.10, 0.30, 0.80),
        InventoryItemsRegionCalibrated = true,
        MinimumBlobArea = 5
    };

    private static FishingStateMachine CreateCatchDetectedMachine(DateTimeOffset start)
    {
        var machine = new FishingStateMachine();
        machine.TryTransition(FishingState.Preparing, "test start", start);
        machine.TryTransition(FishingState.Casting, "test cast", start);
        machine.TryTransition(FishingState.WaitingForBite, "test bite wait", start);
        machine.TryTransition(FishingState.Shaking, "test shake", start);
        machine.TryTransition(FishingState.Reeling, "test reel", start);
        machine.TryTransition(FishingState.CatchDetected, "test catch", start);
        return machine;
    }

    private static ScreenCaptureFrame CreateFrame(
        DateTimeOffset timestamp,
        bool fillItems,
        bool showSellButton = true)
    {
        const int width = 100;
        const int height = 80;
        var pixels = new byte[width * height * 4];

        // The sell button is bright red and appears only while the inventory is open.
        if (showSellButton)
        {
            FillRectangle(pixels, width, 60, 20, 12, 12, 0, 0, 255);
        }

        // The inventory item area is left of the sell button. It changes after a sale.
        if (fillItems)
        {
            FillRectangle(pixels, width, 10, 10, 30, 60, 255, 255, 255);
        }

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels, timestamp, "Roblox", 1234);
    }

    private static void FillRectangle(
        byte[] pixels, int frameWidth, int x, int y, int width, int height,
        byte blue, byte green, byte red)
    {
        for (var row = y; row < y + height; row++)
        {
            for (var column = x; column < x + width; column++)
            {
                var index = (row * frameWidth + column) * 4;
                pixels[index] = blue;
                pixels[index + 1] = green;
                pixels[index + 2] = red;
                pixels[index + 3] = 255;
            }
        }
    }

    private sealed class FakeRobloxInput : IRobloxInput
    {
        public bool IsArmed { get; private set; }
        public int ClientClicks { get; private set; }
        public int InventoryTogglePresses { get; private set; }
        private readonly HashSet<ushort> _pressedKeys = [];

        public void Arm() => IsArmed = true;

        public void SetLeftButton(bool isDown)
        {
            EnsureArmed();
        }

        public void SetKey(ushort virtualKey, bool isDown)
        {
            EnsureArmed();
            if (isDown && virtualKey == 0xC0)
            {
                InventoryTogglePresses++;
            }

            if (isDown)
            {
                _pressedKeys.Add(virtualKey);
            }
            else
            {
                _pressedKeys.Remove(virtualKey);
            }
        }

        public void ClickClient(int clientX, int clientY, ScreenCaptureFrame frame)
        {
            EnsureArmed();
            if (clientX < 0 || clientX >= frame.Width || clientY < 0 || clientY >= frame.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(clientX));
            }

            ClientClicks++;
        }

        public void Disarm()
        {
            IsArmed = false;
            _pressedKeys.Clear();
        }

        private void EnsureArmed()
        {
            if (!IsArmed)
            {
                throw new InvalidOperationException("Fake input is disarmed.");
            }
        }
    }
}
