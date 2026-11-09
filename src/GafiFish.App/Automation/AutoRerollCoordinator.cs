using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using GafiFish.Ocr.Models;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using GafiFish.Windows.Input;

namespace GafiFish.App.Automation;

public enum AutoRerollKind
{
    Appraise,
    Enchant
}

/// <summary>
/// Runs one explicitly armed Appraise or Enchant loop. Every paid action is
/// preceded by a stable, visible prompt and bounded by the user's run limit.
/// A new attempt is never sent until the preceding result is OCR-confirmed.
/// </summary>
public sealed class AutoRerollCoordinator
{
    private enum RunPhase
    {
        EstablishingBaseline,
        ReadyForPrompt,
        AwaitingConfirmation,
        AwaitingResult
    }

    private static readonly Regex CurrencyRegex = new(
        @"(?:(?:C\s*\$|\$)\s*(?<prefix>\d[\d,]*(?:\.\d{1,2})?)|(?<suffix>\d[\d,]*(?:\.\d{1,2})?)\s*C\s*\$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex YesRegex = new(
        @"\byes\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly object _sync = new();
    private readonly IRobloxInput _input;
    private readonly Func<ScreenCaptureFrame> _captureFreshFrame;
    private readonly Func<ScreenCaptureFrame, NormalizedRegion, LocalOcrResult> _recognize;
    private readonly AutoRerollKind _kind;
    private readonly NormalizedRegion _promptRegion;
    private readonly NormalizedRegion? _priceRegion;
    private readonly NormalizedRegion? _confirmRegion;
    private readonly NormalizedRegion _resultRegion;
    private readonly string _targetText;
    private readonly decimal _appraiseBudget;
    private readonly int _enchantRelicLimit;
    private readonly ushort _appraiseFishSlotKey;
    private readonly ushort _enchantRelicSlotKey;
    private readonly ushort _interactionKey;
    private readonly TimeSpan _phaseTimeout = TimeSpan.FromSeconds(15);

    private RunPhase _phase = RunPhase.EstablishingBaseline;
    private DateTimeOffset? _lastProcessedFrameAt;
    private DateTimeOffset? _pendingSince;
    private DateTimeOffset? _waitingForPromptSince;
    private string? _baselineText;
    private string? _candidateText;
    private string? _lastPromptText;
    private string? _lastConfirmText;
    private decimal? _lastObservedCost;
    private decimal? _pendingAppraiseCost;
    private int _candidateFrames;
    private int _stablePromptFrames;
    private int _stableConfirmFrames;
    private bool _started;
    private string _lastStatus = "Auto reroll is stopped.";
    private int _attempts;
    private decimal _currencySpent;

    public AutoRerollCoordinator(
        IRobloxInput input,
        Func<ScreenCaptureFrame> captureFreshFrame,
        Func<ScreenCaptureFrame, NormalizedRegion, LocalOcrResult> recognize,
        AutoRerollKind kind,
        NormalizedRegion promptRegion,
        NormalizedRegion resultRegion,
        string targetText,
        NormalizedRegion? priceRegion = null,
        NormalizedRegion? confirmRegion = null,
        decimal appraiseBudget = 100_000m,
        int enchantRelicLimit = 10,
        ushort appraiseFishSlotKey = 0x31,
        ushort enchantRelicSlotKey = 0x32,
        ushort interactionKey = 0x45)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _captureFreshFrame = captureFreshFrame ?? throw new ArgumentNullException(nameof(captureFreshFrame));
        _recognize = recognize ?? throw new ArgumentNullException(nameof(recognize));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        promptRegion.Validate();
        resultRegion.Validate();
        priceRegion?.Validate();
        confirmRegion?.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetText);
        if (targetText.Trim().Length > 80)
            throw new ArgumentOutOfRangeException(nameof(targetText));
        if (interactionKey == 0)
            throw new ArgumentOutOfRangeException(nameof(interactionKey));

        if (kind == AutoRerollKind.Appraise)
        {
            if (!priceRegion.HasValue || !confirmRegion.HasValue)
                throw new ArgumentException("Appraise requires separate price and Yes-confirmation regions.");
            if (appraiseBudget is < 1 or > 1_000_000_000m)
                throw new ArgumentOutOfRangeException(nameof(appraiseBudget));
            if (appraiseFishSlotKey == 0)
                throw new ArgumentOutOfRangeException(nameof(appraiseFishSlotKey));
        }
        else
        {
            if (enchantRelicLimit is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(enchantRelicLimit));
            if (enchantRelicSlotKey == 0)
                throw new ArgumentOutOfRangeException(nameof(enchantRelicSlotKey));
        }

        _kind = kind;
        _promptRegion = promptRegion;
        _priceRegion = priceRegion;
        _confirmRegion = confirmRegion;
        _resultRegion = resultRegion;
        _targetText = targetText.Trim();
        _appraiseBudget = appraiseBudget;
        _enchantRelicLimit = enchantRelicLimit;
        _appraiseFishSlotKey = appraiseFishSlotKey;
        _enchantRelicSlotKey = enchantRelicSlotKey;
        _interactionKey = interactionKey;
    }

    public bool IsStarted
    {
        get { lock (_sync) return _started; }
    }

    public string LastStatus
    {
        get { lock (_sync) return _lastStatus; }
    }

    public int Attempts
    {
        get { lock (_sync) return _attempts; }
    }

    public decimal CurrencySpent
    {
        get { lock (_sync) return _currencySpent; }
    }

    public int RelicsUsed => _kind == AutoRerollKind.Enchant ? Attempts : 0;

    public void Start()
    {
        lock (_sync)
        {
            if (_started)
                throw new InvalidOperationException("Auto reroll is already running.");

            _input.Arm();
            _started = true;
            _phase = RunPhase.EstablishingBaseline;
            _lastProcessedFrameAt = null;
            _pendingSince = null;
            _waitingForPromptSince = null;
            _baselineText = null;
            _candidateText = null;
            _lastPromptText = null;
            _lastConfirmText = null;
            _lastObservedCost = null;
            _pendingAppraiseCost = null;
            _candidateFrames = 0;
            _stablePromptFrames = 0;
            _stableConfirmFrames = 0;
            _attempts = 0;
            _currencySpent = 0;

            // Equip the chosen item first. Appraise opens its dialogue with E;
            // Enchant waits for the altar's visible E/Enchant prompt.
            PulseKey(_kind == AutoRerollKind.Appraise
                ? _appraiseFishSlotKey
                : _enchantRelicSlotKey);

            if (_kind == AutoRerollKind.Appraise)
                PulseKey(_interactionKey);

            _lastStatus = _kind == AutoRerollKind.Appraise
                ? "Auto Appraise armed · C$ 0 / " + _appraiseBudget.ToString("N0", CultureInfo.InvariantCulture)
                    + " · opening the Appraiser dialogue for the selected fish."
                : "Auto Enchant armed · 0 / " + _enchantRelicLimit + " relics used · waiting for the altar prompt.";
        }
    }

    public void Stop(string reason = "Stopped by the user; game inputs released.")
    {
        lock (_sync)
            Finish(reason);
    }

    /// <summary>
    /// Processes fresh live frames only. Duplicate/out-of-order captures do not
    /// count as evidence. OCR uncertainty never causes another paid action.
    /// </summary>
    public void ProcessFrame(ScreenCaptureFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_sync)
        {
            if (!_started)
                return;

            if (_lastProcessedFrameAt.HasValue && frame.CapturedAt <= _lastProcessedFrameAt.Value)
            {
                _lastStatus = "Ignored duplicate or out-of-order frame; no input sent.";
                return;
            }

            _lastProcessedFrameAt = frame.CapturedAt;
            if (!IsFresh(frame))
            {
                _lastStatus = "Frame was already stale when OCR began; waiting for a fresh capture.";
                return;
            }

            try
            {
                if (_kind == AutoRerollKind.Appraise)
                    ProcessAppraiseFrame(frame);
                else
                    ProcessEnchantFrame(frame);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                or Win32Exception
                or ArgumentException
                or IOException
                or UnauthorizedAccessException)
            {
                Finish("Auto " + _kind + " stopped safely: " + exception.Message);
            }
        }
    }

    public static bool TryParseAppraiseCost(string? text, out decimal cost)
    {
        cost = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var matches = CurrencyRegex.Matches(text);
        if (matches.Count != 1)
            return false;

        var match = matches[0];
        var amount = match.Groups["prefix"].Success
            ? match.Groups["prefix"].Value
            : match.Groups["suffix"].Value;

        return decimal.TryParse(
                amount,
                NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out cost)
            && cost > 0;
    }

    private void ProcessAppraiseFrame(ScreenCaptureFrame frame)
    {
        var result = _recognize(frame, _resultRegion);

        if (_phase == RunPhase.EstablishingBaseline)
        {
            if (ObserveBaseline(result))
            {
                _phase = RunPhase.ReadyForPrompt;
                _waitingForPromptSince = frame.CapturedAt;
                _lastStatus = "Result baseline captured. Waiting for 'Can you appraise this fish?' or 'Can you appraise it again?'.";
            }
            return;
        }

        if (_phase == RunPhase.AwaitingResult)
        {
            ProcessChangedResult(result, frame);
            return;
        }

        if (_phase == RunPhase.AwaitingConfirmation)
        {
            ProcessAppraiseConfirmation(frame);
            return;
        }

        if (PromptWaitTimedOut(frame))
        {
            Finish("Appraise dialogue prompt did not appear within 15 seconds. Check the fish slot, NPC distance and dialogue warm-up.");
            return;
        }

        var prompt = _recognize(frame, _promptRegion);
        var price = _recognize(frame, _priceRegion!.Value);
        if (!prompt.HasText || prompt.Confidence < 0.60
            || !IsAppraisePromptText(prompt.Text)
            || price.Confidence < 0.60 || !price.HasText
            || !TryParseAppraiseCost(price.Text, out var currentCost))
        {
            _lastPromptText = null;
            _lastObservedCost = null;
            _stablePromptFrames = 0;
            _lastStatus = "Waiting for the exact Appraise option and its readable C$ price. No attempt sent.";
            return;
        }

        var normalizedPrompt = Normalize(prompt.Text);
        if (string.Equals(_lastPromptText, normalizedPrompt, StringComparison.Ordinal)
            && _lastObservedCost == currentCost)
        {
            _stablePromptFrames++;
        }
        else
        {
            _lastPromptText = normalizedPrompt;
            _lastObservedCost = currentCost;
            _stablePromptFrames = 1;
        }

        if (_stablePromptFrames < 2)
        {
            _lastStatus = "Appraise option and C$ price stabilising · " + _stablePromptFrames + "/2 frames.";
            return;
        }

        if (_currencySpent + currentCost > _appraiseBudget)
        {
            Finish("Budget protected: this appraisal costs C$ "
                + currentCost.ToString("N0", CultureInfo.InvariantCulture)
                + ", but only C$ " + (_appraiseBudget - _currencySpent).ToString("N0", CultureInfo.InvariantCulture)
                + " remains. The Appraise option was not selected.");
            return;
        }

        ClickAppraisePromptWithFreshCapture(currentCost);
        _pendingAppraiseCost = currentCost;
        _waitingForPromptSince = null;
        _phase = RunPhase.AwaitingConfirmation;
        _pendingSince = frame.CapturedAt;
        _lastConfirmText = null;
        _stableConfirmFrames = 0;
        _lastStatus = "Appraise option selected · quoted price C$ "
            + currentCost.ToString("N0", CultureInfo.InvariantCulture)
            + ". Waiting for the Yes confirmation.";
    }

    private void ProcessAppraiseConfirmation(ScreenCaptureFrame frame)
    {
        if (HasTimedOut(frame))
        {
            Finish("Appraise confirmation did not become readable in time. No paid confirmation click was sent.");
            return;
        }

        var confirm = _recognize(frame, _confirmRegion!.Value);
        if (confirm.Confidence < 0.60 || !confirm.HasText
            || !YesRegex.IsMatch(confirm.Text)
            || !_pendingAppraiseCost.HasValue)
        {
            _lastConfirmText = null;
            _stableConfirmFrames = 0;
            _lastStatus = "Waiting for a clearly readable Yes confirmation. The quoted C$ amount remains reserved; no paid click sent.";
            return;
        }

        var normalizedConfirm = Normalize(confirm.Text);
        if (string.Equals(_lastConfirmText, normalizedConfirm, StringComparison.Ordinal))
            _stableConfirmFrames++;
        else
        {
            _lastConfirmText = normalizedConfirm;
            _stableConfirmFrames = 1;
        }

        if (_stableConfirmFrames < 2)
        {
            _lastStatus = "Yes confirmation stabilising · " + _stableConfirmFrames + "/2 frames.";
            return;
        }

        var currentCost = _pendingAppraiseCost.Value;
        if (_currencySpent + currentCost > _appraiseBudget)
        {
            Finish("Budget protected: the quoted appraisal cost exceeds the remaining C$ budget. No Yes click sent.");
            return;
        }

        ClickAppraiseYesWithFreshCapture(currentCost);
        _pendingAppraiseCost = null;
        _phase = RunPhase.AwaitingResult;
        _pendingSince = frame.CapturedAt;
        _candidateText = null;
        _candidateFrames = 0;
        _lastStatus = "Appraise confirmed once · attempt " + _attempts
            + " · C$ " + currentCost.ToString("N0", CultureInfo.InvariantCulture)
            + " · total C$ " + _currencySpent.ToString("N0", CultureInfo.InvariantCulture)
            + "/" + _appraiseBudget.ToString("N0", CultureInfo.InvariantCulture)
            + ". Waiting for changed fish details.";
    }

    private void ProcessEnchantFrame(ScreenCaptureFrame frame)
    {
        var result = _recognize(frame, _resultRegion);

        if (_phase == RunPhase.EstablishingBaseline)
        {
            if (ObserveBaseline(result))
            {
                _phase = RunPhase.ReadyForPrompt;
                _waitingForPromptSince = frame.CapturedAt;
                _lastStatus = "Enchant result baseline captured. Waiting for the altar's visible Enchant prompt.";
            }
            return;
        }

        if (_phase == RunPhase.AwaitingResult)
        {
            ProcessChangedResult(result, frame);
            return;
        }

        if (_attempts >= _enchantRelicLimit)
        {
            Finish("Relic limit reached · " + _attempts + "/" + _enchantRelicLimit + " uses. No further key press sent.");
            return;
        }

        if (PromptWaitTimedOut(frame))
        {
            Finish("Enchant prompt did not appear within 15 seconds. Check that it is night, the relic slot is correct and the character is at the altar.");
            return;
        }

        var action = _recognize(frame, _promptRegion);
        if (!action.HasText || action.Confidence < 0.60
            || !action.Text.Contains("enchant", StringComparison.OrdinalIgnoreCase))
        {
            _lastPromptText = null;
            _stablePromptFrames = 0;
            _lastStatus = "Waiting for the visible Enchant prompt at the Keeper's Altar.";
            return;
        }

        var normalizedAction = Normalize(action.Text);
        if (string.Equals(_lastPromptText, normalizedAction, StringComparison.Ordinal))
            _stablePromptFrames++;
        else
        {
            _lastPromptText = normalizedAction;
            _stablePromptFrames = 1;
        }

        if (_stablePromptFrames < 2)
        {
            _lastStatus = "Enchant prompt stabilising · " + _stablePromptFrames + "/2 frames.";
            return;
        }

        // The Keeper's Altar uses the configured interaction key (E by default),
        // not a mouse click. The visible prompt must be stable before pressing it.
        PressInteractionWithFreshPrompt();
        _waitingForPromptSince = null;
        _attempts++;
        _phase = RunPhase.AwaitingResult;
        _pendingSince = frame.CapturedAt;
        _candidateText = null;
        _candidateFrames = 0;
        _stablePromptFrames = 0;
        _lastStatus = "Enchant interaction sent once · " + _attempts + "/" + _enchantRelicLimit
            + " relic uses. Waiting for a new enchantment result.";
    }

    private bool ObserveBaseline(LocalOcrResult result)
    {
        if (result.HasText && result.Confidence < 0.55)
        {
            _candidateText = null;
            _candidateFrames = 0;
            _lastStatus = "Waiting for a readable baseline in the result region.";
            return false;
        }

        var normalized = Normalize(result.Text);
        if (string.Equals(_candidateText, normalized, StringComparison.Ordinal))
            _candidateFrames++;
        else
        {
            _candidateText = normalized;
            _candidateFrames = 1;
        }

        if (_candidateFrames < 2)
        {
            _lastStatus = "Capturing a stable pre-action result · 1/2 frames.";
            return false;
        }

        _baselineText = normalized;
        _candidateText = null;
        _candidateFrames = 0;
        return true;
    }

    private void ProcessChangedResult(LocalOcrResult result, ScreenCaptureFrame frame)
    {
        if (HasTimedOut(frame))
        {
            Finish("No new result was OCR-confirmed after the last action. Stopped without retrying or spending more resources.");
            return;
        }

        if (!result.HasText || result.Confidence < 0.60)
        {
            _candidateText = null;
            _candidateFrames = 0;
            _lastStatus = "Waiting for a clear, new result. No repeated action will be sent.";
            return;
        }

        var normalized = Normalize(result.Text);
        if (string.Equals(normalized, _baselineText, StringComparison.Ordinal))
        {
            _candidateText = null;
            _candidateFrames = 0;
            _lastStatus = "Waiting for the result text to change.";
            return;
        }

        if (string.Equals(_candidateText, normalized, StringComparison.Ordinal))
            _candidateFrames++;
        else
        {
            _candidateText = normalized;
            _candidateFrames = 1;
        }

        if (_candidateFrames < 2)
        {
            _lastStatus = "New result detected · confirming on a second distinct frame.";
            return;
        }

        _baselineText = normalized;
        _candidateText = null;
        _candidateFrames = 0;
        _pendingSince = null;

        if (IsTargetMatch(result.Text))
        {
            Finish(_kind == AutoRerollKind.Appraise
                ? "Appraise target found: " + _targetText + ". Total spent: C$ "
                    + _currencySpent.ToString("N0", CultureInfo.InvariantCulture) + "."
                : "Enchant target found: " + _targetText + ". Relics used: "
                    + _attempts + "/" + _enchantRelicLimit + ".");
            return;
        }

        if (_kind == AutoRerollKind.Enchant && _attempts >= _enchantRelicLimit)
        {
            Finish("Relic limit reached · " + _attempts + "/" + _enchantRelicLimit
                + ". Last result did not match '" + _targetText + "'.");
            return;
        }

        if (_kind == AutoRerollKind.Appraise && _currencySpent >= _appraiseBudget)
        {
            Finish("Appraise budget reached · C$ " + _currencySpent.ToString("N0", CultureInfo.InvariantCulture)
                + "/" + _appraiseBudget.ToString("N0", CultureInfo.InvariantCulture)
                + ". Target '" + _targetText + "' was not found.");
            return;
        }

        _lastPromptText = null;
        _stablePromptFrames = 0;
        if (_kind == AutoRerollKind.Appraise)
        {
            // A successful appraisal returns the catch to the player's slot.
            // Re-equip it and open the next NPC dialogue; no paid action occurs
            // until the prompt and its new price/Yes confirmation are visible.
            _waitingForPromptSince = frame.CapturedAt;
            PulseKey(_appraiseFishSlotKey);
            PulseKey(_interactionKey);
            _phase = RunPhase.ReadyForPrompt;
            _lastStatus = "Appraise result confirmed · attempt " + _attempts + " · C$ "
                + _currencySpent.ToString("N0", CultureInfo.InvariantCulture) + "/"
                + _appraiseBudget.ToString("N0", CultureInfo.InvariantCulture)
                + ". Reopening the Appraiser dialogue for the next price.";
        }
        else
        {
            if (_attempts >= _enchantRelicLimit)
            {
                Finish("Relic limit reached · " + _attempts + "/" + _enchantRelicLimit + ".");
                return;
            }

            // Select the configured relic stack again. The next E is only sent
            // after a fresh, stable Enchant prompt appears at the altar.
            _waitingForPromptSince = frame.CapturedAt;
            PulseKey(_enchantRelicSlotKey);
            _phase = RunPhase.ReadyForPrompt;
            _lastStatus = "Enchant result confirmed · " + _attempts + "/" + _enchantRelicLimit
                + ". Relic slot reselected; waiting for the next altar prompt.";
        }
    }

    private bool HasTimedOut(ScreenCaptureFrame frame) =>
        _pendingSince.HasValue && frame.CapturedAt - _pendingSince.Value > _phaseTimeout;

    private bool PromptWaitTimedOut(ScreenCaptureFrame frame) =>
        _waitingForPromptSince.HasValue
        && frame.CapturedAt - _waitingForPromptSince.Value > _phaseTimeout;

    private bool IsTargetMatch(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains(_targetText, StringComparison.OrdinalIgnoreCase);

    private void ClickAppraisePromptWithFreshCapture(decimal expectedCost)
    {
        // The paid price is shown next to the initial Appraise dialogue option.
        // Re-read both the option and exact quote immediately before selecting it.
        var frame = _captureFreshFrame();
        if (!IsFresh(frame))
            throw new InvalidOperationException("Fresh Appraise option capture became stale.");

        var prompt = _recognize(frame, _promptRegion);
        var price = _recognize(frame, _priceRegion!.Value);
        if (!prompt.HasText || prompt.Confidence < 0.60
            || !IsAppraisePromptText(prompt.Text)
            || !price.HasText || price.Confidence < 0.60
            || !TryParseAppraiseCost(price.Text, out var verifiedCost)
            || verifiedCost != expectedCost
            || _currencySpent + verifiedCost > _appraiseBudget)
            throw new InvalidOperationException(
                "The Appraise option or quoted C$ price changed before selection; no action was sent.");

        var bounds = _promptRegion.ToPixelRect(frame.Width, frame.Height);
        _input.ClickClient(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2), frame);
    }

    private void ClickAppraiseYesWithFreshCapture(decimal expectedCost)
    {
        // The follow-up dialogue may only show Yes/No. The C$ quote was read and
        // revalidated on the prior option screen; do not expect it to be repeated.
        var frame = _captureFreshFrame();
        if (!IsFresh(frame))
            throw new InvalidOperationException("Fresh Appraise confirmation capture became stale.");

        var confirm = _recognize(frame, _confirmRegion!.Value);
        if (!confirm.HasText || confirm.Confidence < 0.60
            || !YesRegex.IsMatch(confirm.Text)
            || _currencySpent + expectedCost > _appraiseBudget)
            throw new InvalidOperationException(
                "The Yes confirmation changed or the C$ budget is exhausted; no paid click was sent.");

        var bounds = _confirmRegion.Value.ToPixelRect(frame.Width, frame.Height);

        // Reserve the quoted cost before sending input. If Windows reports an
        // ambiguous input failure, this run stops and cannot retry this budget.
        _currencySpent += expectedCost;
        _attempts++;
        _input.ClickClient(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2), frame);
    }

    private static bool IsAppraisePromptText(string text)
    {
        var normalized = Normalize(text);
        return normalized.Contains("APPRAISE THIS FISH", StringComparison.Ordinal)
            || normalized.Contains("APPRAISE IT AGAIN", StringComparison.Ordinal);
    }

    private void PressInteractionWithFreshPrompt()
    {
        var verificationFrame = _captureFreshFrame();
        if (!IsFresh(verificationFrame))
            throw new InvalidOperationException("Fresh altar prompt capture became stale.");

        var prompt = _recognize(verificationFrame, _promptRegion);
        if (!prompt.HasText || prompt.Confidence < 0.60
            || !prompt.Text.Contains("enchant", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Enchant prompt changed before interaction; no relic use was sent.");

        if (!IsFresh(verificationFrame))
            throw new InvalidOperationException("Altar prompt OCR took too long; no relic was consumed.");

        PulseKey(_interactionKey);
    }

    private void PulseKey(ushort virtualKey)
    {
        if (virtualKey == 0)
            throw new ArgumentOutOfRangeException(nameof(virtualKey));

        _input.SetKey(virtualKey, isDown: true);
        _input.SetKey(virtualKey, isDown: false);
    }

    private static bool IsFresh(ScreenCaptureFrame frame)
    {
        var age = DateTimeOffset.UtcNow - frame.CapturedAt;
        return age >= TimeSpan.Zero && age <= TimeSpan.FromMilliseconds(250);
    }

    private static string Normalize(string? text) =>
        Regex.Replace(text ?? string.Empty, @"\s+", " ").Trim().ToUpperInvariant();

    private void Finish(string status)
    {
        if (!_started)
        {
            _lastStatus = status;
            return;
        }

        _started = false;
        try
        {
            _input.Disarm();
            _lastStatus = status;
        }
        catch (Exception exception)
        {
            _lastStatus = status + " Warning: input release could not be confirmed: " + exception.Message;
        }
    }
}
