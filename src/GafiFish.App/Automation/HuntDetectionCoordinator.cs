using GafiFish.Vision.Detection;

namespace GafiFish.App.Automation;

/// <summary>
/// Debounces the read-only hunt signal. One alert is emitted per visible event;
/// the cue must disappear for several fresh frames before another alert is allowed.
/// </summary>
public sealed class HuntDetectionCoordinator
{
    private readonly int _requiredStableFrames;
    private readonly int _requiredClearFrames;
    private readonly double _minimumConfidence;
    private DateTimeOffset? _lastObservationAt;
    private int _detectedFrames;
    private int _clearFrames;
    private bool _huntIsActive;

    public string LastStatus { get; private set; } = "Hunt alerts are stopped.";
    public bool HuntIsActive => _huntIsActive;

    public HuntDetectionCoordinator(
        int requiredStableFrames = 3,
        int requiredClearFrames = 3,
        double minimumConfidence = 0.70)
    {
        if (requiredStableFrames is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredStableFrames));
        if (requiredClearFrames is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredClearFrames));
        if (!double.IsFinite(minimumConfidence) || minimumConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumConfidence));

        _requiredStableFrames = requiredStableFrames;
        _requiredClearFrames = requiredClearFrames;
        _minimumConfidence = minimumConfidence;
    }

    /// <returns>True once when a new, stable hunt cue appears.</returns>
    public bool ProcessObservation(HuntSignalObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (_lastObservationAt.HasValue && observation.CapturedAt <= _lastObservationAt.Value)
        {
            LastStatus = "Ignored duplicate or out-of-order hunt frame.";
            return false;
        }
        _lastObservationAt = observation.CapturedAt;

        var isConfident = observation.IsDetected
            && double.IsFinite(observation.Confidence)
            && observation.Confidence >= _minimumConfidence;

        if (isConfident)
        {
            _clearFrames = 0;
            _detectedFrames++;

            if (_huntIsActive)
            {
                LastStatus = "Hunt cue remains visible; this event has already been reported.";
                return false;
            }

            if (_detectedFrames < _requiredStableFrames)
            {
                LastStatus = $"Hunt cue stabilising: {_detectedFrames}/{_requiredStableFrames} frames.";
                return false;
            }

            _huntIsActive = true;
            LastStatus = "Hunt detected. Alert emitted; waiting for the cue to disappear before re-arming.";
            return true;
        }

        _detectedFrames = 0;
        _clearFrames++;

        if (_huntIsActive && _clearFrames < _requiredClearFrames)
        {
            LastStatus = $"Hunt cue temporarily absent: {_clearFrames}/{_requiredClearFrames} clear frames.";
            return false;
        }

        if (_huntIsActive)
            _huntIsActive = false;

        LastStatus = "No hunt cue detected; ready for the next event.";
        return false;
    }

    public void Reset()
    {
        _lastObservationAt = null;
        _detectedFrames = 0;
        _clearFrames = 0;
        _huntIsActive = false;
        LastStatus = "Hunt detection reset.";
    }
}
