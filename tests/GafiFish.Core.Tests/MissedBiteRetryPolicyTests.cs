using GafiFish.Core.Automation;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class MissedBiteRetryPolicyTests
{
    [Fact]
    public void Policy_AllowsExactlyTwoRetriesByDefault()
    {
        var policy = new MissedBiteRetryPolicy();

        Assert.True(policy.CanRetry);
        Assert.True(policy.TryConsumeRetry());
        Assert.Equal(1, policy.RetriesUsed);
        Assert.True(policy.TryConsumeRetry());
        Assert.Equal(2, policy.RetriesUsed);
        Assert.False(policy.CanRetry);
        Assert.False(policy.TryConsumeRetry());
        Assert.Equal(2, policy.RetriesUsed);
    }

    [Fact]
    public void Policy_ResetRestoresRetryBudgetAfterReelMarkersAreDetected()
    {
        var policy = new MissedBiteRetryPolicy();

        Assert.True(policy.TryConsumeRetry());
        Assert.True(policy.TryConsumeRetry());

        policy.Reset();

        Assert.Equal(0, policy.RetriesUsed);
        Assert.True(policy.CanRetry);
        Assert.True(policy.TryConsumeRetry());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Policy_RejectsOutOfRangeRetryLimits(int maximumRetries)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MissedBiteRetryPolicy(maximumRetries));
    }

    [Fact]
    public void Policy_CanDisableRetriesExplicitly()
    {
        var policy = new MissedBiteRetryPolicy(maximumRetries: 0);

        Assert.False(policy.CanRetry);
        Assert.False(policy.TryConsumeRetry());
        Assert.Equal(0, policy.RetriesUsed);
    }
}
