using System.IO;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>
/// A bounded reconnect watchdog: stable calibrated prompt, one click, disappearance check,
/// cooldown, and no blind retry while a previous click remains unconfirmed.
/// </summary>
public sealed class ConnectionRecoveryCoordinator
{
    private readonly IRobloxInput _input;
    private readonly ConnectionRecoveryVisionDetector _detector;
    private readonly int _requiredStableFrames;
    private readonly int _requiredAbsentFrames;
    private readonly int _maximumAttempts;
    private readonly TimeSpan _phaseTimeout;
    private readonly TimeSpan _retryDelay;
    private readonly double _maximumCenterDriftPixels;
    private DetectedMarker? _lastCandidate;
    private int _stableFrames;
    private DateTimeOffset? _pendingClickAt;
    private DateTimeOffset? _awaitingSessionIndicatorAt;
    private int _sessionIndicatorFrames;
    private DateTimeOffset _retryAllowedAt = DateTimeOffset.MinValue;
    private int _absentFrames;
    private DateTimeOffset? _lastProcessedFrameAt;
    private bool _started;
    private string _lastStatus = "Connection watchdog is stopped.";

    public bool IsStarted => _started;
    public int AttemptsMade { get; private set; }
    public int MaximumAttempts => _maximumAttempts;
    public string LastStatus => _lastStatus;

    public ConnectionRecoveryCoordinator(
        IRobloxInput input,
        ConnectionRecoveryVisionDetector detector,
        int requiredStableFrames = 3,
        int requiredAbsentFrames = 2,
        int maximumAttempts = 3,
        TimeSpan? phaseTimeout = null,
        TimeSpan? retryDelay = null,
        double maximumCenterDriftPixels = 8)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        if (requiredStableFrames is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredStableFrames));
        if (requiredAbsentFrames is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredAbsentFrames));
        if (maximumAttempts is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        _phaseTimeout = phaseTimeout ?? TimeSpan.FromSeconds(12);
        if (_phaseTimeout < TimeSpan.FromSeconds(1) || _phaseTimeout > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(phaseTimeout));
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(20);
        if (_retryDelay < TimeSpan.FromSeconds(1) || _retryDelay > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        if (!double.IsFinite(maximumCenterDriftPixels) || maximumCenterDriftPixels is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(maximumCenterDriftPixels));

        _requiredStableFrames = requiredStableFrames;
        _requiredAbsentFrames = requiredAbsentFrames;
        _maximumAttempts = maximumAttempts;
        _maximumCenterDriftPixels = maximumCenterDriftPixels;
    }

    public void Start()
    {
        if (_started)
            throw new InvalidOperationException("Connection watchdog is already armed.");

        _input.Arm();
        _lastProcessedFrameAt = null;
        AttemptsMade = 0;
        _pendingClickAt = null;
        _awaitingSessionIndicatorAt = null;
        _sessionIndicatorFrames = 0;
        _retryAllowedAt = DateTimeOffset.MinValue;
        _absentFrames = 0;
        ResetCandidate();
        _started = true;
        _lastStatus = "Connection watchdog armed. Watching for the calibrated reconnect button.";
    }

    public void Stop()
    {
        try { _input.Disarm(); }
        finally
        {
            _started = false;
            _pendingClickAt = null;
            _awaitingSessionIndicatorAt = null;
            _sessionIndicatorFrames = 0;
            _absentFrames = 0;
            _lastProcessedFrameAt = null;
            ResetCandidate();
            _lastStatus = "Connection watchdog stopped; all inputs released.";
        }
    }

    public void ProcessFrame(ScreenCaptureFrame frame, VisionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        if (!_started)
            return;

        // A repeated screenshot is not a second observation. Keep the
        // stability and dismissal counters tied to distinct captured frames.
        if (_lastProcessedFrameAt.HasValue && frame.CapturedAt <= _lastProcessedFrameAt.Value)
        {
            _lastStatus = "Ignored duplicate or out-of-order capture frame; no input was sent.";
            return;
        }

        _lastProcessedFrameAt = frame.CapturedAt;

        try
        {
            var marker = _detector.DetectReconnectButton(frame, profile);
            if (_pendingClickAt.HasValue)
            {
                ProcessPendingClick(frame, marker);
                return;
            }

            if (_awaitingSessionIndicatorAt.HasValue)
            {
                ProcessSessionConfirmation(frame, profile);
                return;
            }

            if (frame.CapturedAt < _retryAllowedAt)
            {
                ResetCandidate();
                _lastStatus = $"Reconnect prompt dismissed; next attempt allowed at {_retryAllowedAt.ToLocalTime():HH:mm:ss}.";
                return;
            }

            if (marker is null || marker.Confidence < 0.65)
            {
                ResetCandidate();
                _lastStatus = $"Watching for reconnect button · {AttemptsMade}/{_maximumAttempts} attempts used.";
                return;
            }

            UpdateCandidate(marker);
            _lastStatus = $"Reconnect button stable {_stableFrames}/{_requiredStableFrames} frames · attempt {AttemptsMade + 1}/{_maximumAttempts}.";
            if (_stableFrames < _requiredStableFrames)
                return;

            // Refuse to click when the supposed "in-game" anchor is already
            // visible alongside the reconnect prompt. This prevents a poor
            // calibration from making the prompt disappearance look successful.
            var sessionAlreadyVisible = _detector.DetectSessionIndicator(frame, profile);
            if (sessionAlreadyVisible is { Confidence: >= 0.65 })
            {
                Fail(
                    "The in-game HUD indicator is already visible with the reconnect prompt. No click was sent; recalibrate with an indicator absent from the reconnect screen.");
                return;
            }

            _input.ClickClient((int)Math.Round(marker.CenterX), (int)Math.Round(marker.CenterY), frame);
            AttemptsMade++;
            _pendingClickAt = frame.CapturedAt;
            _absentFrames = 0;
            ResetCandidate();
            _lastStatus = $"Reconnect click {AttemptsMade}/{_maximumAttempts} sent once; waiting for the calibrated button to disappear.";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ArgumentException
            or InvalidDataException)
        {
            Fail($"Connection watchdog stopped safely: {exception.Message}");
        }
    }

    private void ProcessPendingClick(ScreenCaptureFrame frame, DetectedMarker? marker)
    {
        if (marker is null || marker.Confidence < 0.65)
        {
            _absentFrames++;
            if (_absentFrames >= _requiredAbsentFrames)
            {
                _pendingClickAt = null;
                _absentFrames = 0;
                ResetCandidate();
                _awaitingSessionIndicatorAt = frame.CapturedAt;
                _sessionIndicatorFrames = 0;
                _lastStatus =
                    "Reconnect button disappeared. Waiting for the calibrated in-game HUD indicator; disappearance alone is not proof of reconnection.";
                return;
            }
        }
        else
        {
            _absentFrames = 0;
        }

        if (_pendingClickAt.HasValue && frame.CapturedAt - _pendingClickAt.Value > _phaseTimeout)
        {
            Fail("Reconnect button did not disappear after one click. No retry was sent; watchdog stopped.");
            return;
        }

        _lastStatus = $"Waiting for reconnect prompt to change · {_absentFrames}/{_requiredAbsentFrames} absent frames.";
    }

    private void ProcessSessionConfirmation(ScreenCaptureFrame frame, VisionProfile profile)
    {
        var marker = _detector.DetectSessionIndicator(frame, profile);
        if (marker is not null && marker.Confidence >= 0.65)
        {
            _sessionIndicatorFrames++;
            if (_sessionIndicatorFrames >= _requiredStableFrames)
            {
                Complete(
                    "Roblox session reconnection confirmed by the calibrated in-game HUD indicator across stable frames.");
                return;
            }
        }
        else
        {
            _sessionIndicatorFrames = 0;
        }

        if (_awaitingSessionIndicatorAt.HasValue
            && frame.CapturedAt - _awaitingSessionIndicatorAt.Value > _phaseTimeout)
        {
            _awaitingSessionIndicatorAt = null;
            _sessionIndicatorFrames = 0;

            if (AttemptsMade >= _maximumAttempts)
            {
                Complete(
                    $"Could not confirm the in-game HUD after {_maximumAttempts} reconnect attempts. No more clicks will be sent; check Roblox manually.");
                return;
            }

            _retryAllowedAt = frame.CapturedAt + _retryDelay;
            _lastStatus =
                "The session HUD did not appear before the confirmation timeout. Waiting for a new calibrated reconnect prompt before a bounded retry.";
            return;
        }

        _lastStatus =
            $"Waiting for the calibrated in-game HUD indicator · {_sessionIndicatorFrames}/{_requiredStableFrames} stable frames. Button disappearance alone is not proof.";
    }

    private void UpdateCandidate(DetectedMarker marker)
    {
        if (_lastCandidate is null)
        {
            _lastCandidate = marker;
            _stableFrames = 1;
            return;
        }

        var dx = marker.CenterX - _lastCandidate.CenterX;
        var dy = marker.CenterY - _lastCandidate.CenterY;
        var drift = Math.Sqrt(dx * dx + dy * dy);
        _stableFrames = drift <= _maximumCenterDriftPixels ? _stableFrames + 1 : 1;
        _lastCandidate = marker;
    }

    private void ResetCandidate()
    {
        _lastCandidate = null;
        _stableFrames = 0;
    }

    private void Complete(string status)
    {
        try { _input.Disarm(); }
        finally
        {
            _started = false;
            _pendingClickAt = null;
            _awaitingSessionIndicatorAt = null;
            _sessionIndicatorFrames = 0;
            _lastProcessedFrameAt = null;
            ResetCandidate();
            _lastStatus = status;
        }
    }

    private void Fail(string status) => Complete(status);
}
