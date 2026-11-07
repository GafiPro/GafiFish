using GafiFish.Core.Automation;
using GafiFish.Vision.Detection;

namespace GafiFish.App.Automation;

/// <summary>
/// Rotates between two configured totem slots when a calibrated day/night indicator
/// changes phase. The first stable phase is a baseline and never consumes an item.
/// </summary>
public sealed class DayNightTotemRotationCoordinator
{
    private readonly HotbarItemCoordinator _hotbar;
    private readonly int _rodSlot;
    private readonly int _dayTotemSlot;
    private readonly int _nightTotemSlot;
    private readonly int _requiredStableFrames;
    private DayNightPhase _candidatePhase;
    private DayNightPhase _lastAppliedPhase;
    private int _candidateFrames;
    private DayNightPhase _pendingPhase;
    private string _lastStatus = "Day/night totem rotation is stopped.";

    public DayNightPhase BaselinePhase => _lastAppliedPhase;
    public DayNightPhase PendingPhase => _pendingPhase;
    public string LastStatus => _lastStatus;

    public DayNightTotemRotationCoordinator(
        HotbarItemCoordinator hotbar,
        int rodSlot,
        int dayTotemSlot,
        int nightTotemSlot,
        int requiredStableFrames = 3)
    {
        _hotbar = hotbar ?? throw new ArgumentNullException(nameof(hotbar));
        ValidateSlot(rodSlot, nameof(rodSlot));
        ValidateSlot(dayTotemSlot, nameof(dayTotemSlot));
        ValidateSlot(nightTotemSlot, nameof(nightTotemSlot));

        if (rodSlot == dayTotemSlot || rodSlot == nightTotemSlot)
            throw new ArgumentException("Rod and totem slots must be different.");
        if (dayTotemSlot == nightTotemSlot)
            throw new ArgumentException("Day and night totems must use different hotbar slots.");
        if (requiredStableFrames is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredStableFrames));

        _rodSlot = rodSlot;
        _dayTotemSlot = dayTotemSlot;
        _nightTotemSlot = nightTotemSlot;
        _requiredStableFrames = requiredStableFrames;
    }

    /// <summary>
    /// Observe every live frame, including during fishing. A phase transition is queued
    /// until the state machine reports Preparing and the caller's input cooldown has elapsed.
    /// Returns true only when a hotbar item was actually selected and clicked.
    /// </summary>
    public bool ProcessObservation(DayNightPhaseObservation observation, bool isSafeState)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.Phase == DayNightPhase.Unknown
            || !double.IsFinite(observation.Confidence)
            || observation.Confidence < 0.65)
        {
            _candidatePhase = DayNightPhase.Unknown;
            _candidateFrames = 0;
            _lastStatus = observation.Status;
            return false;
        }

        if (observation.Phase == _candidatePhase)
        {
            _candidateFrames++;
        }
        else
        {
            _candidatePhase = observation.Phase;
            _candidateFrames = 1;
        }

        if (_candidateFrames < _requiredStableFrames)
        {
            _lastStatus =
                $"{observation.Phase} phase stable {_candidateFrames}/{_requiredStableFrames} frames.";
            return false;
        }

        if (_lastAppliedPhase == DayNightPhase.Unknown)
        {
            _lastAppliedPhase = observation.Phase;
            _pendingPhase = DayNightPhase.Unknown;
            _lastStatus =
                $"Initial phase baseline is {_lastAppliedPhase}. No totem was used at startup; waiting for the next phase change.";
            return false;
        }

        if (observation.Phase == _lastAppliedPhase)
        {
            _pendingPhase = DayNightPhase.Unknown;
            _lastStatus = $"Phase unchanged ({_lastAppliedPhase}); no totem use is due.";
            return false;
        }

        _pendingPhase = observation.Phase;
        if (!isSafeState)
        {
            _lastStatus =
                $"{_pendingPhase} phase change confirmed. Waiting for Preparing before using its totem.";
            return false;
        }

        var totemSlot = _pendingPhase == DayNightPhase.Day
            ? _dayTotemSlot
            : _nightTotemSlot;
        _hotbar.UseItem(totemSlot, _rodSlot);
        _lastAppliedPhase = _pendingPhase;
        _pendingPhase = DayNightPhase.Unknown;
        _lastStatus =
            $"Used {_lastAppliedPhase.ToString().ToLowerInvariant()} totem from slot {totemSlot}; rod slot {_rodSlot} restored.";
        return true;
    }

    private static void ValidateSlot(int slot, string name)
    {
        if (slot is < 0 or > 9)
            throw new ArgumentOutOfRangeException(name, "Hotbar slot must be between 0 and 9.");
    }
}
