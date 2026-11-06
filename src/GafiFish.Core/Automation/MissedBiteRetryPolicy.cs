namespace GafiFish.Core.Automation;

/// <summary>
/// Allows bounded automatic recasts by default, with an opt-in unlimited mode.
/// A successful reel-marker detection should reset the retry counter.
/// </summary>
public sealed class MissedBiteRetryPolicy
{
    public const int DefaultMaximumRetries = 2;

    public MissedBiteRetryPolicy(int maximumRetries = DefaultMaximumRetries, bool unlimitedRetries = false)
    {
        if (maximumRetries is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRetries));
        }

        MaximumRetries = maximumRetries;
        UnlimitedRetries = unlimitedRetries;
    }

    public int MaximumRetries { get; }
    public bool UnlimitedRetries { get; }
    public int RetriesUsed { get; private set; }
    public bool CanRetry => UnlimitedRetries || RetriesUsed < MaximumRetries;

    public bool TryConsumeRetry()
    {
        if (!CanRetry)
        {
            return false;
        }

        RetriesUsed++;
        return true;
    }

    public void Reset() => RetriesUsed = 0;
}
