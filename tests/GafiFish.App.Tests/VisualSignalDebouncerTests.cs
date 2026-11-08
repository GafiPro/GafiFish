using GafiFish.App.Automation;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class VisualSignalDebouncerTests
{
    [Fact]
    public void ProcessObservation_RequiresStableDistinctFramesAndEmitsOneAlert()
    {
        var debouncer = new VisualSignalDebouncer(requiredPresentFrames: 2, requiredMissingFrames: 3);
        var start = DateTimeOffset.UtcNow;
        Assert.False(debouncer.ProcessObservation(start, true));
        Assert.True(debouncer.ProcessObservation(start.AddMilliseconds(100), true));
        Assert.False(debouncer.ProcessObservation(start.AddMilliseconds(200), true));
        Assert.True(debouncer.IsActive);
    }

    [Fact]
    public void ProcessObservation_RequiresMissingFramesBeforeRearming()
    {
        var debouncer = new VisualSignalDebouncer(requiredPresentFrames: 2, requiredMissingFrames: 3);
        var start = DateTimeOffset.UtcNow;
        Assert.False(debouncer.ProcessObservation(start, true));
        Assert.True(debouncer.ProcessObservation(start.AddMilliseconds(100), true));
        Assert.False(debouncer.ProcessObservation(start.AddMilliseconds(200), false));
        Assert.False(debouncer.ProcessObservation(start.AddMilliseconds(300), false));
        Assert.False(debouncer.ProcessObservation(start.AddMilliseconds(400), false));
        Assert.False(debouncer.IsActive);
        Assert.False(debouncer.ProcessObservation(start.AddMilliseconds(500), true));
        Assert.True(debouncer.ProcessObservation(start.AddMilliseconds(600), true));
    }

    [Fact]
    public void ProcessObservation_DuplicateOrOutOfOrderFrameDoesNotAdvanceDebounce()
    {
        var debouncer = new VisualSignalDebouncer(requiredPresentFrames: 2);
        var start = DateTimeOffset.UtcNow;
        Assert.False(debouncer.ProcessObservation(start, true));
        Assert.False(debouncer.ProcessObservation(start, true));
        Assert.False(debouncer.ProcessObservation(start.AddMilliseconds(-1), true));
        Assert.True(debouncer.ProcessObservation(start.AddMilliseconds(100), true));
    }

    [Fact]
    public void Reset_ClearsActiveStateAndFrameHistory()
    {
        var debouncer = new VisualSignalDebouncer();
        var start = DateTimeOffset.UtcNow;
        Assert.False(debouncer.ProcessObservation(start, true));
        Assert.True(debouncer.ProcessObservation(start.AddMilliseconds(100), true));
        debouncer.Reset();
        Assert.False(debouncer.IsActive);
        Assert.False(debouncer.ProcessObservation(start, true));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(11, 4)]
    [InlineData(2, 0)]
    [InlineData(2, 21)]
    public void Constructor_RejectsInvalidDebounceThresholds(int present, int missing)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VisualSignalDebouncer(present, missing));
    }
}