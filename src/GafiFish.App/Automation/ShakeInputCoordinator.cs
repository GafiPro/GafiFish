using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

public enum ShakeInputResultKind
{
    NotReady,
    NoPrompt,
    LowConfidence,
    Stabilising,
    WaitingForPromptChange,
    Cooldown,
    Clicked,
    StaleFrame,
    PromptSequenceEnded
}

public sealed record ShakeInputResult(
    ShakeInputResultKind Kind,
    DetectedMarker? Prompt,
    string Reason);

/// <summary>
/// Clicks a calibrated Shake prompt once after it is stable. It waits for the
/// prompt to disappear or move to a new position before permitting another
/// click. This prevents repeated clicks on the same static candidate.
/// </summary>
public sealed class ShakeInputCoordinator
{
    private readonly IRobloxInput _input;
    private readonly ShakeInputSettings _settings;
    private DetectedMarker? _lastClickedPrompt;
    private DetectedMarker? _stablePrompt;
    private int _stableFrames;
    private int _missingFrames;
    private bool _clickedInCurrentSequence;
    private DateTimeOffset _lastProcessedAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastClickAt = DateTimeOffset.MinValue;
    private bool _readyForNextClick = true;

    public ShakeInputCoordinator(
        IRobloxInput input,
        ShakeInputSettings? settings = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _settings = settings ?? new ShakeInputSettings();
        _settings.Validate();
    }

    public ShakeInputResult ProcessFrame(
        ScreenCaptureFrame frame,
        IReadOnlyList<DetectedMarker> prompts,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(prompts);

        var age = (now ?? DateTimeOffset.UtcNow) - frame.CapturedAt;
        if (age < TimeSpan.Zero || age > _settings.MaximumFrameAge
            || (_lastProcessedAt != DateTimeOffset.MinValue && frame.CapturedAt <= _lastProcessedAt))
        {
            return new ShakeInputResult(
                ShakeInputResultKind.StaleFrame,
                null,
                "Shake prompt frame is stale or out of order; no click was sent.");
        }

        _lastProcessedAt = frame.CapturedAt;

        if (prompts.Count == 0)
        {
            return HandleMissingPrompt(
                "No Shake prompt candidate is currently visible.");
        }

        _missingFrames = 0;

        var candidate = prompts
            .Where(prompt => double.IsFinite(prompt.Confidence)
                && prompt.Confidence >= _settings.MinimumConfidence)
            .OrderByDescending(prompt => prompt.Confidence)
            .ThenByDescending(prompt => prompt.Area)
            .FirstOrDefault();

        if (candidate is null)
        {
            _stableFrames = 0;
            _stablePrompt = null;
            return HandleMissingPrompt(
                "Shake candidates are below the confidence threshold.",
                ShakeInputResultKind.LowConfidence);
        }

        if (_lastClickedPrompt is not null
            && Distance(candidate, _lastClickedPrompt) >= _settings.MinimumPromptDisplacementPixels)
        {
            _readyForNextClick = true;
        }

        if (!_readyForNextClick)
        {
            return new ShakeInputResult(
                ShakeInputResultKind.WaitingForPromptChange,
                candidate,
                "The previous prompt is still visible at the same position; waiting for it to move or disappear.");
        }

        if (_stablePrompt is not null
            && Distance(candidate, _stablePrompt) <= _settings.MaximumCandidateDriftPixels)
        {
            _stableFrames++;
        }
        else
        {
            _stablePrompt = candidate;
            _stableFrames = 1;
        }

        if (_stableFrames < _settings.RequiredStableFrames)
        {
            return new ShakeInputResult(
                ShakeInputResultKind.Stabilising,
                candidate,
                $"Shake prompt candidate is stabilising ({_stableFrames}/{_settings.RequiredStableFrames}).");
        }

        if (_lastClickAt != DateTimeOffset.MinValue
            && frame.CapturedAt - _lastClickAt < _settings.MinimumClickInterval)
        {
            return new ShakeInputResult(
                ShakeInputResultKind.Cooldown,
                candidate,
                "Waiting for the minimum interval between Shake clicks.");
        }

        _input.ClickClient(
            (int)Math.Round(candidate.CenterX),
            (int)Math.Round(candidate.CenterY),
            frame);

        _lastClickedPrompt = candidate;
        _lastClickAt = frame.CapturedAt;
        _clickedInCurrentSequence = true;
        _readyForNextClick = false;
        _stablePrompt = null;
        _stableFrames = 0;

        return new ShakeInputResult(
            ShakeInputResultKind.Clicked,
            candidate,
            "Clicked one stable Shake prompt; waiting for its next position.");
    }

    public void Reset()
    {
        _lastClickedPrompt = null;
        _stablePrompt = null;
        _stableFrames = 0;
        _missingFrames = 0;
        _lastClickAt = DateTimeOffset.MinValue;
        _lastProcessedAt = DateTimeOffset.MinValue;
        _clickedInCurrentSequence = false;
        _readyForNextClick = true;
    }

    private ShakeInputResult HandleMissingPrompt(
        string reason,
        ShakeInputResultKind noPromptKind = ShakeInputResultKind.NoPrompt)
    {
        _missingFrames++;
        _stableFrames = 0;
        _stablePrompt = null;

        if (_missingFrames < _settings.RequiredMissingFrames)
        {
            return new ShakeInputResult(noPromptKind, null, reason);
        }

        _readyForNextClick = true;
        if (_clickedInCurrentSequence)
        {
            _clickedInCurrentSequence = false;
            _lastClickedPrompt = null;
            _missingFrames = 0;
            return new ShakeInputResult(
                ShakeInputResultKind.PromptSequenceEnded,
                null,
                "The Shake prompt sequence disappeared; waiting for the reel minigame.");
        }

        return new ShakeInputResult(noPromptKind, null, reason);
    }

    private static double Distance(DetectedMarker left, DetectedMarker right)
    {
        var dx = left.CenterX - right.CenterX;
        var dy = left.CenterY - right.CenterY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
