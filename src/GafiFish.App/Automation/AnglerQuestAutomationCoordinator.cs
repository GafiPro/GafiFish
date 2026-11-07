using GafiFish.Core.Automation;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>
/// Clicks a calibrated Angler Accept or Submit button once after stable
/// visual detection. The tracker advances only after the button disappears
/// on distinct later frames. An ambiguous action stops without retry.
/// </summary>
public sealed class AnglerQuestAutomationCoordinator
{
    private sealed record PendingAction(AnglerQuestButtonKind Kind, DateTimeOffset ClickedAt);

    private readonly IRobloxInput _input;
    private readonly AnglerQuestButtonVisionDetector _detector;
    private readonly Action<AnglerQuestButtonKind, DateTimeOffset> _onConfirmed;
    private readonly int _requiredStableFrames;
    private readonly int _requiredAbsentFrames;
    private readonly TimeSpan _phaseTimeout;
    private readonly double _maximumCenterDriftPixels;

    private AnglerQuestButtonKind? _candidateKind;
    private DetectedMarker? _lastCandidate;
    private int _stableFrames;
    private PendingAction? _pending;
    private int _absentFrames;
    private DateTimeOffset? _lastProcessedFrameAt;
    private bool _started;
    private string _lastStatus = "Angler automation is stopped.";

    public AnglerQuestAutomationCoordinator(
        IRobloxInput input,
        AnglerQuestButtonVisionDetector detector,
        Action<AnglerQuestButtonKind, DateTimeOffset> onConfirmed,
        int requiredStableFrames = 3,
        int requiredAbsentFrames = 2,
        TimeSpan? phaseTimeout = null,
        double maximumCenterDriftPixels = 8)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _onConfirmed = onConfirmed ?? throw new ArgumentNullException(nameof(onConfirmed));

        if (requiredStableFrames is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredStableFrames));
        if (requiredAbsentFrames is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(requiredAbsentFrames));
        _phaseTimeout = phaseTimeout ?? TimeSpan.FromSeconds(6);
        if (_phaseTimeout < TimeSpan.FromSeconds(1) || _phaseTimeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(phaseTimeout));
        if (!double.IsFinite(maximumCenterDriftPixels) || maximumCenterDriftPixels is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(maximumCenterDriftPixels));

        _requiredStableFrames = requiredStableFrames;
        _requiredAbsentFrames = requiredAbsentFrames;
        _maximumCenterDriftPixels = maximumCenterDriftPixels;
    }

    public bool IsStarted => _started;
    public string LastStatus => _lastStatus;
    public AnglerQuestButtonKind? PendingKind => _pending?.Kind;

    public void Start()
    {
        if (_started)
            throw new InvalidOperationException("Angler automation is already started.");

        _input.Arm();
        _lastProcessedFrameAt = null;
        _started = true;
        _lastStatus = "Angler automation armed. Keep the Angler dialogue visible when accepting or handing in a fish.";
    }

    public void Stop()
    {
        try
        {
            _input.Disarm();
        }
        finally
        {
            _started = false;
            _pending = null;
            _absentFrames = 0;
            _lastProcessedFrameAt = null;
            ResetCandidate();
            _lastStatus = "Angler automation stopped; all inputs released.";
        }
    }

    public void ProcessFrame(
        ScreenCaptureFrame frame,
        VisionProfile profile,
        AnglerQuestTracker tracker,
        string? targetSpecies)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tracker);

        if (!_started)
            return;

        if (_lastProcessedFrameAt.HasValue && frame.CapturedAt <= _lastProcessedFrameAt.Value)
        {
            _lastStatus = "Ignored duplicate or out-of-order capture frame; no input was sent.";
            return;
        }

        _lastProcessedFrameAt = frame.CapturedAt;

        try
        {
            if (_pending is not null)
            {
                VerifyPendingAction(frame, profile);
                return;
            }

            var action = tracker.HasCatchToSubmit
                ? AnglerQuestButtonKind.SubmitFish
                : !tracker.IsTracking && tracker.CanStartNewQuest(frame.CapturedAt)
                    && !string.IsNullOrWhiteSpace(targetSpecies)
                    ? AnglerQuestButtonKind.AcceptRequest
                    : (AnglerQuestButtonKind?)null;

            if (!action.HasValue)
            {
                ResetCandidate();
                _lastStatus = tracker.IsTracking
                    ? $"Tracking Angler target: {tracker.TargetSpecies}. Waiting for the requested catch."
                    : tracker.HasCatchToSubmit
                        ? "Target fish caught. Waiting for the calibrated hand-in button."
                        : "Waiting for a ready Angler request button and a target fish name.";
                return;
            }

            if (!profile.AnglerQuestButtons.TryGetValue(action.Value, out var calibration)
                || !calibration.IsCalibrated)
            {
                ResetCandidate();
                _lastStatus = $"Calibrate the {FormatAction(action.Value)} button region and colour first.";
                return;
            }

            var marker = _detector.Detect(frame, profile, action.Value);
            if (marker is null || marker.Confidence < 0.65)
            {
                if (_candidateKind == action)
                    ResetCandidate();
                _lastStatus = $"Waiting for the calibrated {FormatAction(action.Value)} button.";
                return;
            }

            UpdateCandidate(action.Value, marker);
            _lastStatus = $"{FormatAction(action.Value)} button detected · stable {_stableFrames}/{_requiredStableFrames} frames.";

            if (_stableFrames < _requiredStableFrames)
                return;

            _input.ClickClient((int)Math.Round(marker.CenterX), (int)Math.Round(marker.CenterY), frame);
            _pending = new PendingAction(action.Value, frame.CapturedAt);
            _absentFrames = 0;
            ResetCandidate();
            _lastStatus = $"Clicked {FormatAction(action.Value)} once; checking for a visible dialogue change.";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ArgumentException)
        {
            Fail($"Angler automation stopped safely: {exception.Message}");
        }
    }

    private void VerifyPendingAction(ScreenCaptureFrame frame, VisionProfile profile)
    {
        var pending = _pending!;
        var marker = _detector.Detect(frame, profile, pending.Kind);
        if (marker is null || marker.Confidence < 0.65)
        {
            _absentFrames++;
            if (_absentFrames >= _requiredAbsentFrames)
            {
                _onConfirmed(pending.Kind, frame.CapturedAt);
                _pending = null;
                _absentFrames = 0;
                if (pending.Kind == AnglerQuestButtonKind.SubmitFish)
                {
                    Stop();
                    _lastStatus = "Hand-in visually confirmed; the 2-minute cooldown was recorded and inputs were released.";
                }
                else
                {
                    _lastStatus = "Quest acceptance visually confirmed. The target is now being tracked.";
                }
                return;
            }
        }
        else
        {
            _absentFrames = 0;
        }

        if (frame.CapturedAt - pending.ClickedAt > _phaseTimeout)
        {
            Fail($"The {FormatAction(pending.Kind)} button did not visibly change after one click. No retry was sent.");
            return;
        }

        _lastStatus = $"Waiting for visual confirmation of {FormatAction(pending.Kind)} · {_absentFrames}/{_requiredAbsentFrames} absent frames.";
    }

    private void UpdateCandidate(AnglerQuestButtonKind kind, DetectedMarker marker)
    {
        if (_candidateKind != kind || _lastCandidate is null)
        {
            _candidateKind = kind;
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
        _candidateKind = null;
        _lastCandidate = null;
        _stableFrames = 0;
    }

    private void Fail(string reason)
    {
        try
        {
            _input.Disarm();
        }
        finally
        {
            _started = false;
            _pending = null;
            _absentFrames = 0;
            _lastProcessedFrameAt = null;
            ResetCandidate();
            _lastStatus = reason;
        }
    }

    private static string FormatAction(AnglerQuestButtonKind kind) => kind switch
    {
        AnglerQuestButtonKind.AcceptRequest => "Accept Request",
        AnglerQuestButtonKind.SubmitFish => "Submit Fish",
        _ => kind.ToString()
    };
}
