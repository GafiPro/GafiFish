using GafiFish.Core.Aquarium;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

/// <summary>
/// Performs one calibrated Aquarium action at a time. A button must remain
/// stable for several frames before one click is sent. The action cooldown is
/// updated only after that ready-state button disappears on later frames.
/// Ambiguous or unconfirmed clicks stop the module; they are never retried.
/// </summary>
public sealed class AquariumAutomationCoordinator
{
    private sealed record PendingAction(AquariumActionKind Kind, DateTimeOffset ClickedAt);

    private readonly IRobloxInput _input;
    private readonly AquariumButtonVisionDetector _detector;
    private readonly Action<AquariumActionKind, DateTimeOffset> _onConfirmed;
    private readonly int _requiredStableFrames;
    private readonly int _requiredAbsentFrames;
    private readonly TimeSpan _phaseTimeout;
    private readonly double _maximumCenterDriftPixels;

    private AquariumActionKind? _candidateKind;
    private DetectedMarker? _lastCandidate;
    private int _stableFrames;
    private PendingAction? _pending;
    private int _absentFrames;
    private DateTimeOffset? _lastProcessedFrameAt;
    private bool _started;
    private string _lastStatus = "Auto Aquarium is stopped.";

    public AquariumAutomationCoordinator(
        IRobloxInput input,
        AquariumButtonVisionDetector detector,
        Action<AquariumActionKind, DateTimeOffset> onConfirmed,
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
        _phaseTimeout = phaseTimeout ?? TimeSpan.FromSeconds(5);
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
    public AquariumActionKind? PendingKind => _pending?.Kind;

    public void Start()
    {
        if (_started)
            throw new InvalidOperationException("Auto Aquarium is already started.");

        _input.Arm();
        _lastProcessedFrameAt = null;
        _started = true;
        _lastStatus = "Auto Aquarium armed. Keep the Fisch Aquarium panel visible.";
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
            _lastStatus = "Auto Aquarium stopped; all inputs released.";
        }
    }

    public void ProcessFrame(
        ScreenCaptureFrame frame,
        VisionProfile profile,
        AquariumScheduleState schedule,
        AquariumAutomationOptions options)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(options);

        if (!_started)
            return;

        // Repeated or out-of-order captures are not independent observations.
        // Never allow one screenshot to satisfy the stable-button or
        // post-click disappearance thresholds more than once.
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

            foreach (var action in GetEligibleActions(schedule, options, frame.CapturedAt))
            {
                if (!profile.AquariumButtons.TryGetValue(action, out var calibration)
                    || !calibration.IsCalibrated)
                {
                    continue;
                }

                var marker = _detector.Detect(frame, profile, action);
                if (marker is null || marker.Confidence < 0.65)
                {
                    if (_candidateKind == action)
                        ResetCandidate();
                    continue;
                }

                UpdateCandidate(action, marker);
                _lastStatus =
                    $"Ready button detected for {FormatAction(action)} · stable {_stableFrames}/{_requiredStableFrames} frames.";

                if (_stableFrames < _requiredStableFrames)
                    return;

                var x = (int)Math.Round(marker.CenterX);
                var y = (int)Math.Round(marker.CenterY);
                _input.ClickClient(x, y, frame);
                _pending = new PendingAction(action, frame.CapturedAt);
                _absentFrames = 0;
                ResetCandidate();
                _lastStatus = $"Clicked {FormatAction(action)} once; checking that the ready button changed.";
                return;
            }

            ResetCandidate();
            _lastStatus = "Armed · waiting for a calibrated, due Aquarium button. Keep the Aquarium panel open.";
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or ArgumentException)
        {
            Fail($"Auto Aquarium stopped safely: {exception.Message}");
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
                _lastStatus = $"Confirmed {FormatAction(pending.Kind)}; its one-hour cooldown was recorded.";
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

    private void UpdateCandidate(AquariumActionKind action, DetectedMarker marker)
    {
        if (_candidateKind != action || _lastCandidate is null)
        {
            _candidateKind = action;
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

    private static IEnumerable<AquariumActionKind> GetEligibleActions(
        AquariumScheduleState schedule,
        AquariumAutomationOptions options,
        DateTimeOffset now)
    {
        var actions = new List<(AquariumActionKind Kind, DateTimeOffset DueAt)>();
        foreach (var kind in Enum.GetValues<AquariumActionKind>())
        {
            var enabled = kind switch
            {
                AquariumActionKind.FeedGuppiesBest or AquariumActionKind.FeedFishChow
                    or AquariumActionKind.FeedSplashSnacks => options.FeedEnabled,
                AquariumActionKind.RestockGuppiesBest or AquariumActionKind.RestockFishChow
                    or AquariumActionKind.RestockSplashSnacks => options.RestockEnabled,
                AquariumActionKind.ClaimReward => options.ClaimEnabled,
                _ => false
            };
            if (!enabled)
                continue;

            var completedAt = GetLastCompletedAt(schedule, kind);
            if (!completedAt.HasValue)
                continue;

            var evaluation = AquariumCooldownPolicy.Evaluate(completedAt, now);
            if (evaluation.Status == AquariumCooldownStatus.Ready && evaluation.NextEligibleAt.HasValue)
                actions.Add((kind, evaluation.NextEligibleAt.Value));
        }

        return actions.OrderBy(item => item.DueAt).ThenBy(item => item.Kind).Select(item => item.Kind);
    }

    public static DateTimeOffset? GetLastCompletedAt(AquariumScheduleState schedule, AquariumActionKind kind)
    {
        var food = kind switch
        {
            AquariumActionKind.FeedGuppiesBest or AquariumActionKind.RestockGuppiesBest => AquariumFoodType.GuppiesBest,
            AquariumActionKind.FeedFishChow or AquariumActionKind.RestockFishChow => AquariumFoodType.FishChow,
            AquariumActionKind.FeedSplashSnacks or AquariumActionKind.RestockSplashSnacks => AquariumFoodType.SplashSnacks,
            _ => (AquariumFoodType?)null
        };

        if (kind == AquariumActionKind.ClaimReward)
            return schedule.LastClaimedAt;

        var progress = schedule.GetProgress(food!.Value);
        return kind switch
        {
            AquariumActionKind.FeedGuppiesBest or AquariumActionKind.FeedFishChow
                or AquariumActionKind.FeedSplashSnacks => progress.LastFedAt,
            AquariumActionKind.RestockGuppiesBest or AquariumActionKind.RestockFishChow
                or AquariumActionKind.RestockSplashSnacks => progress.LastRestockedAt,
            _ => null
        };
    }

    public static AquariumScheduleState MarkCompleted(
        AquariumScheduleState schedule,
        AquariumActionKind kind,
        DateTimeOffset completedAt)
    {
        if (kind == AquariumActionKind.ClaimReward)
            return schedule.MarkClaimed(completedAt);

        var food = kind switch
        {
            AquariumActionKind.FeedGuppiesBest or AquariumActionKind.RestockGuppiesBest => AquariumFoodType.GuppiesBest,
            AquariumActionKind.FeedFishChow or AquariumActionKind.RestockFishChow => AquariumFoodType.FishChow,
            AquariumActionKind.FeedSplashSnacks or AquariumActionKind.RestockSplashSnacks => AquariumFoodType.SplashSnacks,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return kind switch
        {
            AquariumActionKind.FeedGuppiesBest or AquariumActionKind.FeedFishChow
                or AquariumActionKind.FeedSplashSnacks => schedule.MarkFed(food, completedAt),
            AquariumActionKind.RestockGuppiesBest or AquariumActionKind.RestockFishChow
                or AquariumActionKind.RestockSplashSnacks => schedule.MarkRestocked(food, completedAt),
            _ => schedule
        };
    }

    public static string FormatAction(AquariumActionKind kind) =>
        kind switch
        {
            AquariumActionKind.FeedGuppiesBest => "feed Guppies' Best",
            AquariumActionKind.RestockGuppiesBest => "restock Guppies' Best",
            AquariumActionKind.FeedFishChow => "feed Fish Chow",
            AquariumActionKind.RestockFishChow => "restock Fish Chow",
            AquariumActionKind.FeedSplashSnacks => "feed Splash Snacks",
            AquariumActionKind.RestockSplashSnacks => "restock Splash Snacks",
            AquariumActionKind.ClaimReward => "claim Aquarium reward",
            _ => kind.ToString()
        };
}
