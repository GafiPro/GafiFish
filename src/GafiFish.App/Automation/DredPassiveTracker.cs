namespace GafiFish.App.Automation;

/// <summary>
/// Tracks the documented D.R.E.D. passive cadence from distinct fishing frames.
/// It does not send input: the passive itself locks both fish inside the bar.
/// </summary>
public sealed class DredPassiveTracker
{
    private static readonly TimeSpan ChargeInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan NormalLockDuration = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan OverdriveLockDuration = TimeSpan.FromMilliseconds(2500);

    private DateTimeOffset? _lastObservationAt;
    private DateTimeOffset? _nextChargeAt;
    private DateTimeOffset? _lockedUntil;
    private bool _lastLockWasOverdrive;
    private int _charges;
    private int _overdriveActivations;

    public int ChargeCount => _charges;
    public int OverdriveActivations => _overdriveActivations;
    public string LastStatus { get; private set; } =
        "D.R.E.D. passive tracker is waiting for the first fishing frame.";

    public DredPassiveObservation Process(DateTimeOffset capturedAt)
    {
        if (_lastObservationAt.HasValue && capturedAt <= _lastObservationAt.Value)
        {
            var duplicateLocked = _lockedUntil.HasValue && capturedAt < _lockedUntil.Value;
            return new DredPassiveObservation(
                duplicateLocked,
                duplicateLocked && _lastLockWasOverdrive,
                _charges,
                _nextChargeAt,
                "D.R.E.D. passive ignored a duplicate or out-of-order frame.");
        }

        _lastObservationAt = capturedAt;

        if (!_nextChargeAt.HasValue)
        {
            _nextChargeAt = capturedAt + ChargeInterval;
            LastStatus = "D.R.E.D. passive cadence started; first charge expected after 4 seconds.";
            return Current(capturedAt);
        }

        while (capturedAt >= _nextChargeAt.Value)
        {
            var triggerAt = _nextChargeAt.Value;
            _charges++;

            if (_charges >= 3)
            {
                _charges = 0;
                _lockedUntil = triggerAt + OverdriveLockDuration;
                _lastLockWasOverdrive = true;
                _overdriveActivations++;
            }
            else
            {
                _lockedUntil = triggerAt + NormalLockDuration;
                _lastLockWasOverdrive = false;
            }

            _nextChargeAt = triggerAt + ChargeInterval;
        }

        var observation = Current(capturedAt);
        LastStatus = observation.Status;
        return observation;
    }

    public void Reset()
    {
        _lastObservationAt = null;
        _nextChargeAt = null;
        _lockedUntil = null;
        _lastLockWasOverdrive = false;
        _charges = 0;
        _overdriveActivations = 0;
        LastStatus = "D.R.E.D. passive tracker reset for the next fish.";
    }

    private DredPassiveObservation Current(DateTimeOffset capturedAt)
    {
        var isLocked = _lockedUntil.HasValue && capturedAt < _lockedUntil.Value;
        var isOverdrive = isLocked && _lastLockWasOverdrive;
        var status = isLocked
            ? isOverdrive
                ? "D.R.E.D. OVERDRIVE lock active · Reel input paused for 2.5 seconds."
                : "D.R.E.D. passive lock active · Reel input paused for 0.6 seconds."
            : $"D.R.E.D. passive · {_charges}/3 charges · next charge at {_nextChargeAt:HH:mm:ss.fff}.";

        return new DredPassiveObservation(
            isLocked, isOverdrive, _charges, _nextChargeAt, status);
    }
}

/// <summary>Read-only state for one D.R.E.D. passive observation.</summary>
public sealed record DredPassiveObservation(
    bool IsLocked,
    bool IsOverdrive,
    int ChargeCount,
    DateTimeOffset? NextChargeAt,
    string Status);
