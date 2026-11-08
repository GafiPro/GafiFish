namespace GafiFish.App.Automation;

/// <summary>
/// Emits one alert after a signal is visible for several distinct frames and
/// rearms only after it has been absent for several distinct frames. This is
/// read-only alert debouncing: it does not send game input or infer collection.
/// </summary>
public sealed class VisualSignalDebouncer
{
    private readonly int _requiredPresentFrames;
    private readonly int _requiredMissingFrames;
    private DateTimeOffset? _lastProcessedAt;
    private int _presentFrames;
    private int _missingFrames;
    private bool _isActive;

    public VisualSignalDebouncer(int requiredPresentFrames = 2, int requiredMissingFrames = 4)
    {
        if (requiredPresentFrames is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredPresentFrames));
        if (requiredMissingFrames is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(requiredMissingFrames));
        _requiredPresentFrames = requiredPresentFrames;
        _requiredMissingFrames = requiredMissingFrames;
    }

    public bool IsActive => _isActive;

    public bool ProcessObservation(DateTimeOffset capturedAt, bool isPresent)
    {
        if (_lastProcessedAt.HasValue && capturedAt <= _lastProcessedAt.Value)
            return false;
        _lastProcessedAt = capturedAt;

        if (isPresent)
        {
            _missingFrames = 0;
            if (_isActive)
                return false;
            _presentFrames++;
            if (_presentFrames < _requiredPresentFrames)
                return false;
            _presentFrames = 0;
            _isActive = true;
            return true;
        }

        _presentFrames = 0;
        if (!_isActive)
            return false;
        _missingFrames++;
        if (_missingFrames >= _requiredMissingFrames)
        {
            _missingFrames = 0;
            _isActive = false;
        }
        return false;
    }

    public void Reset()
    {
        _lastProcessedAt = null;
        _presentFrames = 0;
        _missingFrames = 0;
        _isActive = false;
    }
}