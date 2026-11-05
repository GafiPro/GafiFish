namespace GafiFish.Core.Scheduling;

/// <summary>
/// Tracks the Sell Anywhere interval without performing UI actions.
/// The UI/automation layer must wait for a safe fishing state and visually
/// validate the inventory button before executing a sale.
/// </summary>
public sealed class PeriodicSellSchedule
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(10);

    private readonly TimeSpan _interval;
    private DateTimeOffset? _nextDueAt;
    private bool _isRunning;
    private bool _salePending;
    private bool _saleInProgress;

    public PeriodicSellSchedule(TimeSpan? interval = null)
    {
        _interval = interval ?? DefaultInterval;
        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");
        }
    }

    public DateTimeOffset? NextDueAt => _nextDueAt;
    public bool IsRunning => _isRunning;
    public bool SalePending => _salePending;
    public bool SaleInProgress => _saleInProgress;

    public void Start(DateTimeOffset now)
    {
        _isRunning = true;
        _salePending = false;
        _saleInProgress = false;
        _nextDueAt = now + _interval;
    }

    public void Stop()
    {
        _isRunning = false;
        _saleInProgress = false;
        _nextDueAt = null;
        // Keep a pending request visible so the caller can report it in the
        // final status instead of pretending that a sale completed.
    }

    /// <summary>Call periodically from the app's scheduler.</summary>
    public bool Poll(DateTimeOffset now)
    {
        if (!_isRunning || _salePending || _saleInProgress || _nextDueAt is null)
        {
            return _salePending;
        }

        if (now >= _nextDueAt.Value)
        {
            _salePending = true;
        }

        return _salePending;
    }

    /// <summary>
    /// Begins a sale only after the current fishing cycle has ended and the
    /// caller has confirmed the Roblox window is foreground and usable.
    /// </summary>
    public bool TryBeginSale(bool isSafeToSell)
    {
        if (!_isRunning || !_salePending || _saleInProgress || !isSafeToSell)
        {
            return false;
        }

        _saleInProgress = true;
        return true;
    }

    /// <summary>Call only after the inventory UI confirms the sale completed.</summary>
    public void CompleteSale(DateTimeOffset now)
    {
        if (!_saleInProgress)
        {
            throw new InvalidOperationException("There is no sale in progress.");
        }

        _saleInProgress = false;
        _salePending = false;
        _nextDueAt = now + _interval;
    }

    /// <summary>
    /// Leaves the request pending but stops the scheduler. The caller should
    /// show an error and require user intervention; never repeat blind clicks.
    /// </summary>
    public void FailSale()
    {
        _saleInProgress = false;
        _isRunning = false;
        _salePending = true;
    }
}
