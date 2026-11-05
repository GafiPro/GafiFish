using GafiFish.Core.Automation;
using GafiFish.Core.Scheduling;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>
/// Drives Sell Anywhere only after a fishing cycle has ended. It waits for a
/// calibrated, stable button, clicks exactly once, verifies a visual change in
/// the inventory list, then closes the inventory. Ambiguous UI states fail
/// closed and disarm all input.
/// </summary>
public sealed class SellAnywhereCoordinator
{
    private readonly IRobloxInput _input;
    private readonly SellAnywhereVisionDetector _detector;
    private readonly PeriodicSellSchedule _schedule;
    private readonly FishingStateMachine _stateMachine;
    private readonly SellAnywhereSettings _settings;

    private DateTimeOffset _phaseStartedAt;
    private ScreenCaptureFrame? _beforeSaleFrame;
    private DetectedMarker? _lastButton;
    private int _stableButtonFrames;
    private string _lastStatus = "Sell Anywhere is stopped.";
    private bool _started;

    public SellAnywhereCoordinator(
        IRobloxInput input,
        SellAnywhereVisionDetector detector,
        PeriodicSellSchedule schedule,
        FishingStateMachine stateMachine,
        SellAnywhereSettings? settings = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _settings = settings ?? new SellAnywhereSettings();
        _settings.Validate();
    }

    public string LastStatus => _lastStatus;
    public bool IsStarted => _started;
    public DateTimeOffset? NextSaleDueAt => _schedule.NextDueAt;

    public void Start(DateTimeOffset now)
    {
        if (_started)
        {
            throw new InvalidOperationException("Sell Anywhere is already started.");
        }

        _input.Arm();
        _schedule.Start(now);
        _started = true;
        _lastStatus = "Sell Anywhere scheduler started; first sale is due in 10 minutes.";
    }

    /// <summary>
    /// Call once per fresh Roblox frame. isFishingCycleSafe must only be true
    /// after a catch is complete (never during Casting, Shake, or Reeling).
    /// </summary>
    public void ProcessFrame(
        ScreenCaptureFrame frame,
        VisionProfile profile,
        bool isFishingCycleSafe)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);

        if (!_started)
        {
            _lastStatus = "Sell Anywhere is stopped.";
            return;
        }

        try
        {
            if (_stateMachine.CurrentState == FishingState.CatchDetected)
            {
                _ = _schedule.Poll(frame.CapturedAt);
                if (_schedule.SalePending && isFishingCycleSafe)
                {
                    BeginPendingSale(profile, frame.CapturedAt);
                }
            }

            switch (_stateMachine.CurrentState)
            {
                case FishingState.OpeningInventory:
                    ObserveInventoryOpening(frame, profile);
                    break;
                case FishingState.SellingInventory:
                    ClickStableSellButton(frame, profile);
                    break;
                case FishingState.VerifyingSale:
                    VerifySale(frame, profile);
                    break;
                case FishingState.ClosingInventory:
                    // The inventory has been closed and the schedule reset.
                    break;
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ArgumentException)
        {
            Fail($"Sell Anywhere stopped safely: {exception.Message}");
        }
    }

    public void Stop()
    {
        if (!_started)
        {
            _input.Disarm();
            return;
        }

        _schedule.Stop();
        _input.Disarm();
        _started = false;
        _beforeSaleFrame = null;
        _lastButton = null;
        _stableButtonFrames = 0;
        _lastStatus = "Sell Anywhere stopped; all inputs released.";
    }

    private void BeginPendingSale(VisionProfile profile, DateTimeOffset now)
    {
        if (!profile.IsSellButtonCalibrated || !profile.IsInventoryVerificationCalibrated)
        {
            Fail("Calibrate the Sell Anywhere button and inventory item-list area before enabling automatic sales.");
            return;
        }

        if (!_schedule.TryBeginSale(isSafeToSell: true))
        {
            return;
        }

        if (!_stateMachine.TryTransition(FishingState.SellPending, "Ten-minute Sell Anywhere interval reached.", now))
        {
            Fail("The fishing state machine rejected the pending sale.");
            return;
        }

        if (!_stateMachine.TryTransition(FishingState.OpeningInventory, "Fishing cycle finished; opening inventory.", now))
        {
            Fail("The fishing state machine rejected the inventory-opening transition.");
            return;
        }

        ToggleInventory();
        _phaseStartedAt = now;
        ResetButtonStability();
        _lastStatus = "Waiting for the calibrated inventory and Sell Anywhere button.";
    }

    private void ObserveInventoryOpening(ScreenCaptureFrame frame, VisionProfile profile)
    {
        var observation = _detector.DetectButton(frame, profile);
        if (!observation.IsCalibrated || observation.SellButton is null
            || observation.SellButton.Confidence < _settings.MinimumButtonConfidence)
        {
            ResetButtonStability();
            CheckTimeout(frame.CapturedAt, "The Sell Anywhere button did not appear in the calibrated inventory region.");
            return;
        }

        UpdateStableButton(observation.SellButton);
        _lastStatus = $"Inventory detected; stabilising Sell Anywhere button ({_stableButtonFrames}/{_settings.RequiredStableButtonFrames}).";

        if (_stableButtonFrames >= _settings.RequiredStableButtonFrames)
        {
            if (!_stateMachine.TryTransition(
                    FishingState.SellingInventory,
                    "Sell Anywhere button is stable across multiple frames.",
                    frame.CapturedAt))
            {
                Fail("The fishing state machine rejected the selling transition.");
                return;
            }

            _phaseStartedAt = frame.CapturedAt;
            ResetButtonStability();
        }
    }

    private void ClickStableSellButton(ScreenCaptureFrame frame, VisionProfile profile)
    {
        var observation = _detector.DetectButton(frame, profile);
        if (!observation.IsCalibrated || observation.SellButton is null
            || observation.SellButton.Confidence < _settings.MinimumButtonConfidence)
        {
            ResetButtonStability();
            CheckTimeout(frame.CapturedAt, "Sell Anywhere button disappeared before the click.");
            return;
        }

        UpdateStableButton(observation.SellButton);
        _lastStatus = $"Sell button stable ({_stableButtonFrames}/{_settings.RequiredStableButtonFrames}).";

        if (_stableButtonFrames < _settings.RequiredStableButtonFrames)
        {
            CheckTimeout(frame.CapturedAt, "Sell Anywhere button did not remain stable long enough.");
            return;
        }

        _beforeSaleFrame = frame;
        var x = (int)Math.Round(observation.SellButton.CenterX);
        var y = (int)Math.Round(observation.SellButton.CenterY);

        // Exactly one click is sent. The coordinator never retries on an
        // uncertain result.
        _input.ClickClient(x, y, frame);

        if (!_stateMachine.TryTransition(
                FishingState.VerifyingSale,
                "Sell Anywhere button clicked once; waiting for inventory change.",
                frame.CapturedAt))
        {
            Fail("The fishing state machine rejected the sale-verification transition.");
            return;
        }

        _phaseStartedAt = frame.CapturedAt;
        _lastStatus = "Sell button clicked once; verifying the inventory change.";
    }

    private void VerifySale(ScreenCaptureFrame frame, VisionProfile profile)
    {
        if (_beforeSaleFrame is null)
        {
            Fail("No pre-sale inventory frame was saved; sale cannot be verified.");
            return;
        }

        var elapsed = frame.CapturedAt - _phaseStartedAt;
        if (elapsed < _settings.VerificationDelay)
        {
            return;
        }

        var changedRatio = _detector.MeasureInventoryChange(
            _beforeSaleFrame,
            frame,
            profile.InventoryItemsRegion);

        if (changedRatio >= _settings.MinimumInventoryChangeRatio)
        {
            ToggleInventory();

            if (!_stateMachine.TryTransition(
                    FishingState.ClosingInventory,
                    $"Inventory changed by {changedRatio:P1}; sale visually verified.",
                    frame.CapturedAt))
            {
                Fail("The fishing state machine rejected the inventory-closing transition.");
                return;
            }

            _schedule.CompleteSale(frame.CapturedAt);

            if (!_stateMachine.TryTransition(
                    FishingState.Preparing,
                    "Inventory closed; ready to resume fishing.",
                    frame.CapturedAt))
            {
                Fail("The fishing state machine rejected the return-to-fishing transition.");
                return;
            }

            _beforeSaleFrame = null;
            _lastStatus = $"Sale visually verified ({changedRatio:P1} inventory change). Next sale due in 10 minutes.";
            return;
        }

        CheckTimeout(frame.CapturedAt,
            $"No sufficient inventory change was detected (observed {changedRatio:P1}); sale was not confirmed.");
    }

    private void UpdateStableButton(DetectedMarker marker)
    {
        if (_lastButton is not null)
        {
            var dx = marker.CenterX - _lastButton.CenterX;
            var dy = marker.CenterY - _lastButton.CenterY;
            var drift = Math.Sqrt(dx * dx + dy * dy);
            _stableButtonFrames = drift <= _settings.MaximumStableCenterDriftPixels
                ? _stableButtonFrames + 1
                : 1;
        }
        else
        {
            _stableButtonFrames = 1;
        }

        _lastButton = marker;
    }

    private void ResetButtonStability()
    {
        _lastButton = null;
        _stableButtonFrames = 0;
    }

    private void CheckTimeout(DateTimeOffset now, string reason)
    {
        if (now - _phaseStartedAt > _settings.PhaseTimeout)
        {
            Fail(reason);
        }
        else
        {
            _lastStatus = reason;
        }
    }

    private void ToggleInventory()
    {
        _input.SetKey(_settings.InventoryToggleVirtualKey, isDown: true);
        _input.SetKey(_settings.InventoryToggleVirtualKey, isDown: false);
    }

    private void Fail(string reason)
    {
        _schedule.FailSale();
        _input.Disarm();
        _started = false;
        _beforeSaleFrame = null;
        ResetButtonStability();

        if (_stateMachine.CurrentState != FishingState.Error)
        {
            _ = _stateMachine.TryTransition(FishingState.Error, reason);
        }

        _lastStatus = reason;
    }
}
