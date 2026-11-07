using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>
/// One-shot, calibrated UI action. It needs three distinct stable frames,
/// sends exactly one normal Windows click, then disarms immediately. There
/// is no automatic retry or chaining because Appraise/Enchant can consume
/// currency or items and Treasure Appraiser choices are user decisions.
/// </summary>
public sealed class FischUiActionCoordinator
{
    private readonly IRobloxInput _input;
    private readonly FischUiActionVisionDetector _detector;
    private readonly int _requiredStableFrames;
    private readonly double _minimumConfidence;
    private readonly double _maximumCenterDriftPixels;
    private readonly TimeSpan _maximumFrameAge;
    private readonly TimeSpan _maximumWait;

    private FischUiActionKind? _action;
    private DetectedMarker? _lastCandidate;
    private int _stableFrames;
    private DateTimeOffset? _lastProcessedFrameAt;
    private DateTimeOffset _startedAt;
    private bool _started;
    private bool _completed;
    private string _lastStatus = "One-shot Fisch action is stopped.";

    public FischUiActionCoordinator(
        IRobloxInput input,
        FischUiActionVisionDetector detector,
        int requiredStableFrames = 3,
        double minimumConfidence = 0.70,
        double maximumCenterDriftPixels = 10,
        TimeSpan? maximumFrameAge = null,
        TimeSpan? maximumWait = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        if (requiredStableFrames is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredStableFrames));
        if (!double.IsFinite(minimumConfidence) || minimumConfidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumConfidence));
        if (!double.IsFinite(maximumCenterDriftPixels) || maximumCenterDriftPixels is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(maximumCenterDriftPixels));
        _maximumFrameAge = maximumFrameAge ?? TimeSpan.FromMilliseconds(250);
        _maximumWait = maximumWait ?? TimeSpan.FromSeconds(20);
        if (_maximumFrameAge <= TimeSpan.Zero || _maximumWait < TimeSpan.FromSeconds(1)
            || _maximumWait > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(maximumWait));
        _requiredStableFrames = requiredStableFrames;
        _minimumConfidence = minimumConfidence;
        _maximumCenterDriftPixels = maximumCenterDriftPixels;
    }

    public bool IsStarted => _started;
    public bool HasCompleted => _completed;
    public FischUiActionKind? Action => _action;
    public string LastStatus => _lastStatus;
    public int StableFrames => _stableFrames;

    public void Start(FischUiActionKind action, VisionProfile profile, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (_started)
            throw new InvalidOperationException("A Fisch UI action is already armed.");
        if (!profile.FischUiActions.TryGetValue(action, out var calibration) || !calibration.IsCalibrated)
            throw new InvalidOperationException($"Calibrate the region and enabled-button colour for {action.DisplayName()} first.");

        _input.Arm();
        _action = action;
        _lastCandidate = null;
        _stableFrames = 0;
        _lastProcessedFrameAt = null;
        _startedAt = now ?? DateTimeOffset.UtcNow;
        _completed = false;
        _started = true;
        _lastStatus = $"Armed for one click: {action.DisplayName()}. Waiting for its calibrated button to remain stable.";
    }

    public void ProcessFrame(ScreenCaptureFrame frame, VisionProfile profile, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        if (!_started || !_action.HasValue)
            return;

        var currentTime = now ?? DateTimeOffset.UtcNow;
        var age = currentTime - frame.CapturedAt;
        if (age < TimeSpan.Zero || age > _maximumFrameAge)
        {
            _lastStatus = "Ignored a stale or future capture frame; waiting for a fresh Roblox observation.";
            return;
        }
        if (_lastProcessedFrameAt.HasValue && frame.CapturedAt <= _lastProcessedFrameAt.Value)
        {
            _lastStatus = "Ignored duplicate or out-of-order frame; it cannot count toward the click threshold.";
            return;
        }
        _lastProcessedFrameAt = frame.CapturedAt;

        if (currentTime - _startedAt > _maximumWait)
        {
            Fail("No stable calibrated button appeared before the timeout. No click was sent.");
            return;
        }

        try
        {
            var candidate = _detector.Detect(frame, profile, _action.Value);
            if (candidate is null || !double.IsFinite(candidate.Confidence)
                || candidate.Confidence < _minimumConfidence)
            {
                ResetCandidate();
                _lastStatus = "Waiting for the enabled button in the calibrated region. No input sent.";
                return;
            }

            if (_lastCandidate is null)
            {
                _lastCandidate = candidate;
                _stableFrames = 1;
            }
            else
            {
                var dx = candidate.CenterX - _lastCandidate.CenterX;
                var dy = candidate.CenterY - _lastCandidate.CenterY;
                var drift = Math.Sqrt(dx * dx + dy * dy);
                _stableFrames = drift <= _maximumCenterDriftPixels ? _stableFrames + 1 : 1;
                _lastCandidate = candidate;
            }

            _lastStatus = $"{_action.Value.DisplayName()} · stable {_stableFrames}/{_requiredStableFrames} frames.";
            if (_stableFrames < _requiredStableFrames)
                return;

            var x = (int)Math.Round(candidate.CenterX);
            var y = (int)Math.Round(candidate.CenterY);
            _input.ClickClient(x, y, frame);
            _input.Disarm();
            _started = false;
            _completed = true;
            _lastStatus = $"One click sent for {_action.Value.DisplayName()}. Verify the result in Fisch; no retry or follow-up click will occur.";
            ResetCandidate();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ArgumentException)
        {
            Fail($"One-shot action stopped safely: {exception.Message}");
        }
    }

    public void Stop(string reason = "One-shot action stopped by the user; all inputs released.")
    {
        try { _input.Disarm(); }
        finally
        {
            _started = false;
            _completed = false;
            _lastProcessedFrameAt = null;
            ResetCandidate();
            _lastStatus = reason;
        }
    }

    private void Fail(string reason)
    {
        try { _input.Disarm(); }
        finally
        {
            _started = false;
            _completed = false;
            _lastProcessedFrameAt = null;
            ResetCandidate();
            _lastStatus = reason;
        }
    }

    private void ResetCandidate()
    {
        _lastCandidate = null;
        _stableFrames = 0;
    }
}
