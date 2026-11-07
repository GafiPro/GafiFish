using GafiFish.Core.Casting;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

public enum CastInputResultKind
{
    Idle,
    WaitingForMeter,
    WaitingForTarget,
    Released,
    Cancelled,
    TimedOut,
    StaleObservation
}

public sealed record CastInputResult(
    CastInputResultKind Kind,
    PerfectCastDecision? Decision,
    string Reason);

/// <summary>
/// Holds the ordinary Windows left mouse button during a calibrated cast and
/// releases it when the visual Perfect Cast controller says Release.
/// The coordinator has a strict maximum hold duration and releases on every
/// cancellation path. It does not equip a rod or start a session by itself.
/// </summary>
public sealed class CastInputCoordinator
{
    private readonly IRobloxInput _input;
    private readonly PerfectCastController _controller;
    private readonly TimeSpan _maximumHoldDuration;
    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastObservationAt = DateTimeOffset.MinValue;
    private bool _isHolding;

    public CastInputCoordinator(
        IRobloxInput input,
        PerfectCastController? controller = null,
        TimeSpan? maximumHoldDuration = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _controller = controller ?? new PerfectCastController();
        _maximumHoldDuration = maximumHoldDuration ?? TimeSpan.FromSeconds(8);

        if (_maximumHoldDuration <= TimeSpan.Zero
            || _maximumHoldDuration > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumHoldDuration));
        }
    }

    public bool IsHolding => _isHolding;

    public void BeginCast(DateTimeOffset now)
    {
        if (_isHolding)
        {
            throw new InvalidOperationException("A cast is already in progress.");
        }

        _input.SetLeftButton(true);
        _startedAt = now;
        _lastObservationAt = DateTimeOffset.MinValue;
        _isHolding = true;
    }

    public CastInputResult ProcessObservation(
        CastMeterObservation? observation,
        DateTimeOffset now)
    {
        if (!_isHolding)
        {
            return new CastInputResult(
                CastInputResultKind.Idle,
                null,
                "No cast is currently being charged.");
        }

        if (observation is not null)
        {
            if (observation.CapturedAt <= _lastObservationAt)
            {
                Release();
                return new CastInputResult(
                    CastInputResultKind.StaleObservation,
                    null,
                    "Perfect Cast observation is duplicated or out of order; mouse released.");
            }

            _lastObservationAt = observation.CapturedAt;
        }

        var elapsed = now - _startedAt;
        if (elapsed < TimeSpan.Zero || elapsed > _maximumHoldDuration)
        {
            Release();
            return new CastInputResult(
                CastInputResultKind.TimedOut,
                null,
                "Cast meter did not reach a safe release decision before the timeout; mouse released.");
        }

        if (observation is null)
        {
            return new CastInputResult(
                CastInputResultKind.WaitingForMeter,
                null,
                "Waiting for calibrated cast-meter markers.");
        }

        var decision = _controller.Evaluate(observation, now);
        switch (decision.Kind)
        {
            case PerfectCastDecisionKind.Wait:
                return new CastInputResult(
                    CastInputResultKind.WaitingForTarget,
                    decision,
                    decision.Reason);

            case PerfectCastDecisionKind.Release:
                Release();
                return new CastInputResult(
                    CastInputResultKind.Released,
                    decision,
                    decision.Reason);

            case PerfectCastDecisionKind.Cancel:
            default:
                Release();
                return new CastInputResult(
                    CastInputResultKind.Cancelled,
                    decision,
                    $"Cast cancelled safely: {decision.Reason}");
        }
    }

    public void Stop()
    {
        Release();
    }

    private void Release()
    {
        if (!_isHolding)
        {
            return;
        }

        try
        {
            _input.SetLeftButton(false);
        }
        finally
        {
            _isHolding = false;
        }
    }
}
