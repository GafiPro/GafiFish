using GafiFish.Core.Automation;
using GafiFish.Windows.Input;
using GafiFish.Core.Control;
using GafiFish.Vision.Detection;
using GafiFish.Vision.Tracking;

namespace GafiFish.App.Automation;

/// <summary>
/// Applies reel-controller decisions to the guarded Windows input service.
/// It only controls the mouse while the state machine is Reeling and fresh
/// detections for both markers are available. Any uncertainty releases the
/// mouse button; the controller's Maintain decision preserves the prior state.
/// </summary>
public sealed class ReelInputCoordinator
{
    private readonly IRobloxInput _input;
    private readonly ReelController _controller;
    private readonly FishMotionTracker _controlMotionTracker = new();
    private bool _chargeReleaseConsumed;
    private DateTimeOffset? _lastChargeObservationAt;

    public ReelInputCoordinator(
        IRobloxInput input,
        ReelController? controller = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _controller = controller ?? new ReelController();
    }

    public bool IsArmed => _input.IsArmed;

    public void Arm() => _input.Arm();

    public ReelControlDecision Apply(
        FishingState state,
        DetectedMarker? fishMarker,
        DetectedMarker? controlMarker,
        FishMotionEstimate? motion,
        RodChargeMeterObservation? chargeMeter = null,
        bool requireDualFishCoverage = false)
    {
        if (state != FishingState.Reeling)
        {
            _controlMotionTracker.Reset();
            ResetChargeMeterCycle();
            _input.SetLeftButton(false);
            return new ReelControlDecision(
                ReelCommand.Neutral, 0, 0,
                "Reel control is inactive outside the Reeling state.");
        }

        if (fishMarker is null || controlMarker is null || motion is null || !motion.IsFresh)
        {
            _controlMotionTracker.Reset();
            _input.SetLeftButton(false);
            return new ReelControlDecision(
                ReelCommand.Neutral, 0, 0,
                "A marker or fresh motion estimate is missing; mouse input released.");
        }

        var hasHorizontalSpan = ControlBarGeometry.TryGetHorizontalSpanAtY(
            controlMarker, fishMarker.CenterY, out var horizontalSpan);
        var controlPositionX = hasHorizontalSpan ? horizontalSpan.CenterX : controlMarker.CenterX;
        var controlWidth = hasHorizontalSpan ? horizontalSpan.Width : controlMarker.Bounds.Width;
        var fishInsideControlBar = hasHorizontalSpan
            && fishMarker.CenterX >= horizontalSpan.Left
            && fishMarker.CenterX <= horizontalSpan.Right;

        // Track the control bar independently from the fish. This makes the
        // controller work with bars that translate across the minigame area,
        // using relative horizontal velocity rather than assuming the bar is static.
        var controlMotion = _controlMotionTracker.Update(
            controlMarker with { CenterX = controlPositionX }, motion.ObservedAt);
        if (controlMotion is null || !controlMotion.IsFresh)
        {
            _input.SetLeftButton(false);
            return new ReelControlDecision(
                ReelCommand.Neutral, 0, 0,
                "A fresh control-bar movement estimate is missing; mouse input released.");
        }
        var relativeVelocity = motion.VelocityPixelsPerSecond
            - controlMotion.VelocityPixelsPerSecond;

        if (requireDualFishCoverage
            && (!hasHorizontalSpan || fishMarker.Bounds.Width > controlWidth))
        {
            _input.SetLeftButton(false);
            return new ReelControlDecision(
                ReelCommand.Neutral, fishMarker.CenterX - controlPositionX, controlWidth,
                "The rotated control bar does not cover both fish targets; input released.");
        }

        var confidence = Math.Min(
            motion.Confidence,
            Math.Min(controlMotion.Confidence,
                Math.Min(fishMarker.Confidence, controlMarker.Confidence)));

        // Charge meters can be cyclical (for example, a meter that fills,
        // drains and then starts filling again). Release once per rising cycle,
        // and only re-arm after a fresh reading has returned to 20% or below.
        // This prevents a high meter reading from forcing release on every frame.
        if (chargeMeter is { IsUsable: true }
            && IsChargeObservationFresh(chargeMeter, motion.ObservedAt)
            && (!_lastChargeObservationAt.HasValue
                || chargeMeter.CapturedAt > _lastChargeObservationAt.Value))
        {
            _lastChargeObservationAt = chargeMeter.CapturedAt;

            if (_chargeReleaseConsumed)
            {
                if (chargeMeter.Progress <= 0.20)
                    _chargeReleaseConsumed = false;
            }

            if (!_chargeReleaseConsumed && chargeMeter.Progress >= 0.80 && fishInsideControlBar)
            {
                _chargeReleaseConsumed = true;
                _input.SetLeftButton(false);
                return new ReelControlDecision(
                    ReelCommand.Release,
                    fishMarker.CenterX - controlPositionX,
                    chargeMeter.Progress,
                    "Rod charge crossed the 80% release window while the fish was inside the control bar; one release was sent for this cycle.");
            }
        }

        var decision = _controller.Evaluate(
            motion.PositionX,
            controlPositionX,
            relativeVelocity,
            confidence,
            isFresh: true);

        try
        {
            switch (decision.Command)
            {
                case ReelCommand.Hold:
                    _input.SetLeftButton(true);
                    break;
                case ReelCommand.Release:
                case ReelCommand.Neutral:
                    _input.SetLeftButton(false);
                    break;
                case ReelCommand.Maintain:
                    // Keep the current button state while the target remains in the deadband.
                    break;
                default:
                    _input.SetLeftButton(false);
                    break;
            }

            return decision;
        }
        catch
        {
            _input.Disarm();
            throw;
        }
    }

    private static bool IsChargeObservationFresh(
        RodChargeMeterObservation observation,
        DateTimeOffset reelObservedAt)
    {
        var age = reelObservedAt - observation.CapturedAt;
        return age >= TimeSpan.Zero && age <= TimeSpan.FromMilliseconds(180);
    }

    private void ResetChargeMeterCycle()
    {
        _chargeReleaseConsumed = false;
        _lastChargeObservationAt = null;
    }

    public void Stop()
    {
        _controlMotionTracker.Reset();
        ResetChargeMeterCycle();
        _input.Disarm();
    }
}
