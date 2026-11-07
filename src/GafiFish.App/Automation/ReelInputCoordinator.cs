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
        RodChargeMeterObservation? chargeMeter = null)
    {
        if (state != FishingState.Reeling)
        {
            _input.SetLeftButton(false);
            return new ReelControlDecision(
                ReelCommand.Neutral, 0, 0,
                "Reel control is inactive outside the Reeling state.");
        }

        if (fishMarker is null || controlMarker is null || motion is null || !motion.IsFresh)
        {
            _input.SetLeftButton(false);
            return new ReelControlDecision(
                ReelCommand.Neutral, 0, 0,
                "A marker or fresh motion estimate is missing; mouse input released.");
        }

        var confidence = Math.Min(
            motion.Confidence,
            Math.Min(fishMarker.Confidence, controlMarker.Confidence));

        // Charge-meter rods grant a progress bonus when the meter is released
        // above 80% while the fish is inside the calibrated control bar. If
        // the charge meter is uncertain, fall back to normal Reel control.
        if (chargeMeter is { IsUsable: true, Progress: >= 0.80 }
            && fishMarker.CenterX >= controlMarker.Bounds.Left
            && fishMarker.CenterX <= controlMarker.Bounds.Right)
        {
            _input.SetLeftButton(false);
            return new ReelControlDecision(
                ReelCommand.Release,
                fishMarker.CenterX - controlMarker.CenterX,
                chargeMeter.Progress,
                "Rod charge reached 80% while the fish was inside the control bar; released for the calibrated charge bonus.");
        }

        var decision = _controller.Evaluate(
            motion.PositionX,
            controlMarker.CenterX,
            motion.VelocityPixelsPerSecond,
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

    public void Stop() => _input.Disarm();
}
