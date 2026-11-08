using GafiFish.Core.Automation;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>
/// Runs the Jinglestar passive's ten hold/release pulses using user-confirmed
/// per-rod timings. It runs once per Reeling state and yields back to normal
/// Reel control immediately after the tenth release.
/// </summary>
public sealed class JinglestarSequenceCoordinator
{
    public const int RequiredPulses = 10;
    public const int MinimumIntervalMilliseconds = 20;
    public const int MaximumIntervalMilliseconds = 500;

    private readonly IRobloxInput _input;
    private readonly TimeSpan _holdDuration;
    private readonly TimeSpan _releaseDuration;

    private DateTimeOffset? _lastFrameAt;
    private DateTimeOffset? _nextTransitionAt;
    private DateTimeOffset? _nextSequenceAt;
    private bool _sequenceUsedThisCatch;
    private bool _sequenceActive;
    private bool _mouseDown;
    private int _completedPulses;
    private string _lastStatus = "Jinglestar sequence is stopped.";

    public JinglestarSequenceCoordinator(
        IRobloxInput input,
        int holdMilliseconds,
        int releaseMilliseconds)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        ValidateInterval(holdMilliseconds, nameof(holdMilliseconds));
        ValidateInterval(releaseMilliseconds, nameof(releaseMilliseconds));
        _holdDuration = TimeSpan.FromMilliseconds(holdMilliseconds);
        _releaseDuration = TimeSpan.FromMilliseconds(releaseMilliseconds);
    }

    public bool IsActive => _sequenceActive;
    public int CompletedPulses => _completedPulses;
    public string LastStatus => _lastStatus;

    /// <summary>
    /// Returns true while the pulse sequence owns the mouse input. Duplicate
    /// frames never advance the timer or cause another input transition.
    /// </summary>
    public bool ProcessFrame(
        DateTimeOffset capturedAt,
        FishingState state,
        bool allowSequence = true)
    {
        if (state != FishingState.Reeling)
        {
            ResetForNextCatch();
            _lastFrameAt = capturedAt;
            _lastStatus = "Jinglestar sequence waiting for the next Reeling state.";
            return false;
        }

        if (_lastFrameAt.HasValue && capturedAt <= _lastFrameAt.Value)
        {
            _lastStatus = "Jinglestar sequence ignored a duplicate or out-of-order frame.";
            return _sequenceActive;
        }

        _lastFrameAt = capturedAt;

        if (!allowSequence)
        {
            if (_sequenceActive)
            {
                if (_mouseDown)
                    SetMouseButton(false);
                _mouseDown = false;
                _sequenceActive = false;
                _nextTransitionAt = null;
                _sequenceUsedThisCatch = true;
                _lastStatus = "Jinglestar sequence cancelled because fresh fish/bar detections were lost; mouse released.";
            }
            else if (!_sequenceUsedThisCatch)
            {
                _lastStatus = "Jinglestar waiting for fresh, high-confidence fish/bar detections.";
            }

            return false;
        }

        if (!_sequenceUsedThisCatch)
        {
            if (_nextSequenceAt.HasValue && capturedAt < _nextSequenceAt.Value)
            {
                _lastStatus = $"Jinglestar · 10/10 complete · cooldown until {_nextSequenceAt.Value:HH:mm:ss.fff}.";
                return false;
            }

            _nextSequenceAt = null;
            _sequenceUsedThisCatch = true;
            _sequenceActive = true;
            _completedPulses = 0;
            SetMouseButton(true);
            _mouseDown = true;
            _nextTransitionAt = capturedAt + _holdDuration;
            _lastStatus = $"Jinglestar · pulse 1/{RequiredPulses} · hold phase.";
            return true;
        }

        if (!_sequenceActive || !_nextTransitionAt.HasValue || capturedAt < _nextTransitionAt.Value)
            return _sequenceActive;

        if (_mouseDown)
        {
            SetMouseButton(false);
            _mouseDown = false;
            _completedPulses++;

            if (_completedPulses >= RequiredPulses)
            {
                _sequenceActive = false;
                _sequenceUsedThisCatch = false;
                _nextTransitionAt = null;
                _nextSequenceAt = capturedAt.AddSeconds(2);
                _lastStatus = "Jinglestar · 10/10 pulses completed; 2-second cooldown before the next sequence.";
                return false;
            }

            _nextTransitionAt = capturedAt + _releaseDuration;
            _lastStatus = $"Jinglestar · {_completedPulses}/{RequiredPulses} pulses · release phase.";
            return true;
        }

        SetMouseButton(true);
        _mouseDown = true;
        _nextTransitionAt = capturedAt + _holdDuration;
        _lastStatus = $"Jinglestar · {_completedPulses + 1}/{RequiredPulses} pulses · hold phase.";
        return true;
    }

    public void Stop()
    {
        try
        {
            if (_mouseDown)
                _input.SetLeftButton(false);
        }
        finally
        {
            _lastFrameAt = null;
            _nextTransitionAt = null;
            _nextSequenceAt = null;
            _sequenceUsedThisCatch = false;
            _sequenceActive = false;
            _mouseDown = false;
            _completedPulses = 0;
            _lastStatus = "Jinglestar sequence stopped; mouse input released.";
        }
    }

    private void ResetForNextCatch()
    {
        if (_mouseDown)
            _input.SetLeftButton(false);

        _nextTransitionAt = null;
        _nextSequenceAt = null;
        _sequenceUsedThisCatch = false;
        _sequenceActive = false;
        _mouseDown = false;
        _completedPulses = 0;
    }

    private void SetMouseButton(bool isDown)
    {
        try
        {
            _input.SetLeftButton(isDown);
        }
        catch
        {
            try { _input.SetLeftButton(false); }
            catch { /* Best-effort release; Disarm is the fallback. */ }

            try { _input.Disarm(); }
            finally
            {
                _sequenceActive = false;
                _mouseDown = false;
                _nextTransitionAt = null;
                _lastStatus = "Jinglestar input failed; the input service was disarmed.";
            }

            throw;
        }
    }

    private static void ValidateInterval(int milliseconds, string name)
    {
        if (milliseconds is < MinimumIntervalMilliseconds or > MaximumIntervalMilliseconds)
            throw new ArgumentOutOfRangeException(name,
                $"Jinglestar timing must be between {MinimumIntervalMilliseconds} and {MaximumIntervalMilliseconds} ms.");
    }
}
