namespace GafiFish.Core.Automation;

/// <summary>
/// Bounds automatic recasts after the cast succeeds but no bite is detected.
/// A successful reel-marker detection should reset the policy.
/// </summary>
public sealed class MissedBiteRetryPolicy
{
    public const int DefaultMaximumRetries = 2;

    public MissedBiteRetryPolicy(int maximumRetries = DefaultMaximumRetries)
    {
        if (maximumRetries is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRetries));
        }

        MaximumRetries = maximumRetries;
    }

    public int MaximumRetries { get; }
    public int RetriesUsed { get; private set; }
    public bool CanRetry => RetriesUsed < MaximumRetries;

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
