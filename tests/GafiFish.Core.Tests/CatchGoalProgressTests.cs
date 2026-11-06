using GafiFish.Core.Automation;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class CatchGoalProgressTests
{
    [Fact]
    public void RecordMatch_CompletesOnlyAtRequestedCount()
    {
        var progress = new CatchGoalProgress(3);

        Assert.False(progress.RecordMatch());
        Assert.Equal(1, progress.Caught);
        Assert.Equal(2, progress.Remaining);
        Assert.False(progress.RecordMatch());
        Assert.True(progress.RecordMatch());
        Assert.True(progress.IsComplete);
        Assert.Equal(0, progress.Remaining);
    }

    [Fact]
    public void RecordMatch_DoesNotIncreaseBeyondGoal()
    {
        var progress = new CatchGoalProgress(1);
        Assert.True(progress.RecordMatch());
        Assert.True(progress.RecordMatch());
        Assert.Equal(1, progress.Caught);
    }

    [Fact]
    public void Reset_ClearsProgressButKeepsGoal()
    {
        var progress = new CatchGoalProgress(2);
        progress.RecordMatch();
        progress.Reset();

        Assert.Equal(2, progress.Goal);
        Assert.Equal(0, progress.Caught);
        Assert.Equal(2, progress.Remaining);
        Assert.False(progress.IsComplete);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Constructor_RejectsOutOfRangeGoal(int goal)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CatchGoalProgress(goal));
    }
}
