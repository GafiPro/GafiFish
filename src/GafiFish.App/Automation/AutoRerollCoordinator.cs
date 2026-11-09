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
/// Runs one explicitly armed Appraise or Enchant loop. Each click is a normal
/// Windows input, bounded by the configured C$ budget or relic count. A new
/// attempt is never sent until the preceding result is OCR-confirmed.
/// </summary>
public sealed class AutoRerollCoordinator
{
    private enum RunPhase
    {
        EstablishingBaseline,
        Ready,
        AwaitingResult
    }

    private static readonly Regex CurrencyRegex = new(
        @"(?:(?:C\s*\$|\$)\s*(?<prefix>\d[\d,]*(?:\.\d{1,2})?)|(?<suffix>\d[\d,]*(?:\.\d{1,2})?)\s*C\s*\$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly object _sync = new();
    private readonly IRobloxInput _input;
    private readonly Func<ScreenCaptureFrame> _captureFreshFrame;
    private readonly Func<ScreenCaptureFrame, NormalizedRegion, LocalOcrResult> _recognize;
    private readonly AutoRerollKind _kind;
    private readonly NormalizedRegion _clickRegion;
    private readonly NormalizedRegion? _priceRegion;
    private readonly NormalizedRegion _resultRegion;
    private readonly string _targetText;
    private readonly decimal _appraiseBudget;
    private readonly int _enchantRelicLimit;
    private readonly TimeSpan _resultTimeout = TimeSpan.FromSeconds(12);
    private readonly TimeSpan _enchantRepeatDelay = TimeSpan.FromMilliseconds(900);

    private RunPhase _phase = RunPhase.EstablishingBaseline;
    private DateTimeOffset? _lastProcessedFrameAt;
    private DateTimeOffset? _pendingSince;
    private DateTimeOffset _nextAttemptAt;
    private string? _baselineText;
    private string? _candidateText;
    private int _candidateFrames;
    private string? _lastButtonText;
    private decimal? _lastObservedCost;
    private int _stableButtonFrames;
    private bool _started;
    private string _lastStatus = "Auto reroll is stopped.";
    private int _attempts;
    private decimal _currencySpent;

    public AutoRerollCoordinator(
        IRobloxInput input,
        Func<ScreenCaptureFrame> captureFreshFrame,
        Func<ScreenCaptureFrame, NormalizedRegion, LocalOcrResult> recognize,
        AutoRerollKind kind,
        NormalizedRegion clickRegion,
        NormalizedRegion resultRegion,
        string targetText,
        NormalizedRegion? priceRegion = null,
        decimal appraiseBudget = 100_000m,
        int enchantRelicLimit = 10)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _captureFreshFrame = captureFreshFrame ?? throw new ArgumentNullException(nameof(captureFreshFrame));
        _recognize = recognize ?? throw new ArgumentNullException(nameof(recognize));
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        clickRegion.Validate();
        resultRegion.Validate();
        priceRegion?.Validate();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetText);
        if (targetText.Trim().Length > 80)
            throw new ArgumentOutOfRangeException(nameof(targetText));

        if (kind == AutoRerollKind.Appraise)
        {
            if (!priceRegion.HasValue)
                throw new ArgumentException("Appraise requires a calibrated price region.", nameof(priceRegion));
            if (appraiseBudget is < 1 or > 1_000_000_000m)
                throw new ArgumentOutOfRangeException(nameof(appraiseBudget));
        }
        else if (enchantRelicLimit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(enchantRelicLimit));
        }

        _kind = kind;
        _clickRegion = clickRegion;
        _priceRegion = priceRegion;
        _resultRegion = resultRegion;
        _targetText = targetText.Trim();
        _appraiseBudget = appraiseBudget;
        _enchantRelicLimit = enchantRelicLimit;
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
            _baselineText = null;
            _candidateText = null;
            _candidateFrames = 0;
            _lastButtonText = null;
            _lastObservedCost = null;
            _stableButtonFrames = 0;
            _attempts = 0;
            _currencySpent = 0;
            _lastStatus = _kind == AutoRerollKind.Appraise
                ? "Auto Appraise armed · C$ 0 / " + _appraiseBudget.ToString("N0", CultureInfo.InvariantCulture)
                    + " spent · target: " + _targetText + "."
                : "Auto Enchant armed · 0 / " + _enchantRelicLimit + " relics used · target: " + _targetText + ".";
        }
    }

    public void Stop(string reason = "Stopped by the user; game inputs released.")
    {
        lock (_sync)
            Finish(reason);
    }

    /// <summary>
    /// Processes fresh live frames only. Replayed frames do not count as new
    /// evidence. OCR uncertainty never causes another click.
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
                _phase = RunPhase.Ready;
                _lastStatus = "Appraise result baseline captured. Waiting for the Appraise button and exact C$ cost.";
            }
            return;
        }

        if (_phase == RunPhase.AwaitingResult)
        {
            ProcessChangedResult(result, frame);
            return;
        }

        if (_attempts == 0 && IsTargetMatch(result.Text))
        {
            Finish("Target already visible before the first attempt (" + _targetText + "); no C$ spent.");
            return;
        }

        var button = _recognize(frame, _clickRegion);
        if (button.Confidence < 0.58
            || !button.HasText
            || !button.Text.Contains("appraise", StringComparison.OrdinalIgnoreCase))
        {
            _stableButtonFrames = 0;
            _lastButtonText = null;
            _lastObservedCost = null;
            _lastStatus = "Waiting for a clearly recognised Appraise button.";
            return;
        }

        var price = _recognize(frame, _priceRegion!.Value);
        if (price.Confidence < 0.58
            || !price.HasText
            || !TryParseAppraiseCost(price.Text, out var currentCost))
        {
            _stableButtonFrames = 0;
            _lastButtonText = null;
            _lastObservedCost = null;
            _lastStatus = "Appraise button found, but its exact C$ price is unclear. No click sent.";
            return;
        }

        var normalizedButton = Normalize(button.Text);
        if (string.Equals(_lastButtonText, normalizedButton, StringComparison.Ordinal)
            && _lastObservedCost == currentCost)
        {
            _stableButtonFrames++;
        }
        else
        {
            _lastButtonText = normalizedButton;
            _lastObservedCost = currentCost;
            _stableButtonFrames = 1;
        }

        if (_stableButtonFrames < 2)
        {
            _lastStatus = "Appraise button and price stabilising · " + _stableButtonFrames + "/2 frames.";
            return;
        }

        if (_currencySpent + currentCost > _appraiseBudget)
        {
            Finish("Budget protected: next appraisal costs C$ " + currentCost.ToString("N0", CultureInfo.InvariantCulture)
                + ", but only C$ " + (_appraiseBudget - _currencySpent).ToString("N0", CultureInfo.InvariantCulture)
                + " remains. Total spent: C$ " + _currencySpent.ToString("N0", CultureInfo.InvariantCulture) + ".");
            return;
        }

        ClickCentreWithFreshCapture();
        // Count the full displayed price as soon as the click is sent. An
        // ambiguous response never refunds budget or permits a blind retry.
        _currencySpent += currentCost;
        _attempts++;
        _pendingSince = frame.CapturedAt;
        _phase = RunPhase.AwaitingResult;
        _candidateText = null;
        _candidateFrames = 0;
        _stableButtonFrames = 0;
        _lastStatus = "Appraise click sent once · attempt " + _attempts
            + " · C$ " + currentCost.ToString("N0", CultureInfo.InvariantCulture)
            + " · total C$ " + _currencySpent.ToString("N0", CultureInfo.InvariantCulture)
            + "/" + _appraiseBudget.ToString("N0", CultureInfo.InvariantCulture)
            + ". Waiting for a changed result.";
    }

    private void ProcessEnchantFrame(ScreenCaptureFrame frame)
    {
        var result = _recognize(frame, _resultRegion);
        if (_phase == RunPhase.EstablishingBaseline)
        {
            if (ObserveBaseline(result))
            {
                _phase = RunPhase.Ready;
                _nextAttemptAt = frame.CapturedAt;
                _lastStatus = "Enchant result baseline captured. Waiting for the visible Enchant action.";
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
            Finish("Relic limit reached · " + _attempts + "/" + _enchantRelicLimit + " uses. No further click sent.");
            return;
        }

        if (frame.CapturedAt < _nextAttemptAt)
        {
            _lastStatus = "Waiting before the next relic use · " + _attempts + "/" + _enchantRelicLimit + " used.";
            return;
        }

        var action = _recognize(frame, _clickRegion);
        if (action.Confidence < 0.58
            || !action.HasText
            || !action.Text.Contains("enchant", StringComparison.OrdinalIgnoreCase))
        {
            _stableButtonFrames = 0;
            _lastButtonText = null;
            _lastStatus = "Waiting for a clearly recognised Enchant action at the calibrated altar.";
            return;
        }

        var normalizedAction = Normalize(action.Text);
        if (string.Equals(_lastButtonText, normalizedAction, StringComparison.Ordinal))
            _stableButtonFrames++;
        else
        {
            _lastButtonText = normalizedAction;
            _stableButtonFrames = 1;
        }

        if (_stableButtonFrames < 2)
        {
            _lastStatus = "Enchant action stabilising · " + _stableButtonFrames + "/2 frames.";
            return;
        }

        ClickCentreWithFreshCapture();
        _attempts++;
        _pendingSince = frame.CapturedAt;
        _phase = RunPhase.AwaitingResult;
        _candidateText = null;
        _candidateFrames = 0;
        _stableButtonFrames = 0;
        _lastStatus = "Relic use sent once · " + _attempts + "/" + _enchantRelicLimit
            + ". Waiting for a new enchant result before any further click.";
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
        if (_pendingSince.HasValue && frame.CapturedAt - _pendingSince.Value > _resultTimeout)
        {
            Finish("No new result was OCR-confirmed after the last click. Stopped without retrying.");
            return;
        }

        if (!result.HasText || result.Confidence < 0.60)
        {
            _candidateText = null;
            _candidateFrames = 0;
            _lastStatus = "Waiting for a clear, new result. No repeated click will be sent.";
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

        _phase = RunPhase.Ready;
        _nextAttemptAt = frame.CapturedAt + _enchantRepeatDelay;
        _lastButtonText = null;
        _lastObservedCost = null;
        _stableButtonFrames = 0;
        _lastStatus = _kind == AutoRerollKind.Appraise
            ? "Result confirmed · attempt " + _attempts + " · C$ "
                + _currencySpent.ToString("N0", CultureInfo.InvariantCulture) + "/"
                + _appraiseBudget.ToString("N0", CultureInfo.InvariantCulture)
                + ". Target not found; checking the next displayed price."
            : "Result confirmed · " + _attempts + "/" + _enchantRelicLimit
                + " relics used. Target not found; waiting before the next use.";
    }

    private bool IsTargetMatch(string text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains(_targetText, StringComparison.OrdinalIgnoreCase);

    private void ClickCentreWithFreshCapture()
    {
        // OCR can take longer than the input freshness window. Capture a new
        // frame immediately before sending input; the input service still
        // verifies the armed PID and current foreground process.
        var freshFrame = _captureFreshFrame();
        if (!IsFresh(freshFrame))
            throw new InvalidOperationException("Fresh capture became stale before the click.");

        var bounds = _clickRegion.ToPixelRect(freshFrame.Width, freshFrame.Height);
        var x = bounds.X + (bounds.Width / 2);
        var y = bounds.Y + (bounds.Height / 2);
        _input.ClickClient(x, y, freshFrame);
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
