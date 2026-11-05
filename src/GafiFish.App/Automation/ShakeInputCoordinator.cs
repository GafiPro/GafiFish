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
    StaleFrame
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
        IReadOnlyList<DetectedMarker> prompts)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(prompts);

        var age = DateTimeOffset.UtcNow - frame.CapturedAt;
        if (age < TimeSpan.Zero || age > _settings.MaximumFrameAge)
        {
            return new ShakeInputResult(
                ShakeInputResultKind.StaleFrame,
                null,
                "Shake prompt frame is stale; no click was sent.");
        }

        if (prompts.Count == 0)
        {
            _missingFrames++;
            _stableFrames = 0;
            _stablePrompt = null;

            if (_missingFrames >= _settings.RequiredMissingFrames)
            {
                _readyForNextClick = true;
            }

            return new ShakeInputResult(
                ShakeInputResultKind.NoPrompt,
                null,
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
            return new ShakeInputResult(
                ShakeInputResultKind.LowConfidence,
                null,
                "Shake candidates are below the confidence threshold.");
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
        _readyForNextClick = true;
    }

    private static double Distance(DetectedMarker left, DetectedMarker right)
    {
        var dx = left.CenterX - right.CenterX;
        var dy = left.CenterY - right.CenterY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
