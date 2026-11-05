using GafiFish.Core.Models;
using GafiFish.Core.Services;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class SessionSummaryBuilderTests
{
    [Fact]
    public void Build_ShowsOnlyTop25_WhenSessionHasMoreThan25Catches()
    {
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);
        var catches = Enumerable.Range(0, 40)
            .Select(i => new FishCatch(
                Guid.NewGuid(),
                start.AddMinutes(i),
                $"Fish {i}",
                i == 39 ? FishRarity.Secret : FishRarity.Common,
                null,
                1 + i,
                10 + i,
                0.99))
            .ToArray();

        var summary = SessionSummaryBuilder.Build(catches, start, start.AddHours(1));

        Assert.Equal(40, summary.TotalCatches);
        Assert.Equal(25, summary.TopCatches.Count);
        Assert.True(summary.WasTruncated);
        Assert.Equal("Fish 39", summary.TopCatches[0].Species);
    }

    [Fact]
    public void Build_OrdersByRarityThenMutationThenWeight()
    {
        var start = DateTimeOffset.UtcNow;
        var catches = new[]
        {
            CreateCatch(start, "Common heavy", FishRarity.Common, null, 100),
            CreateCatch(start, "Mythical", FishRarity.Mythical, null, 2),
            CreateCatch(start, "Legendary mutated", FishRarity.Legendary, "Shiny", 5)
        };

        var summary = SessionSummaryBuilder.Build(catches, start, start.AddMinutes(5));

        Assert.Equal(
            new[] { "Mythical", "Legendary mutated", "Common heavy" },
            summary.TopCatches.Select(c => c.Species));
    }

    [Fact]
    public void Build_UsesZeroRateForZeroDuration()
    {
        var now = DateTimeOffset.UtcNow;
        var summary = SessionSummaryBuilder.Build([], now, now);

        Assert.Equal(0, summary.CatchesPerHour);
    }

    private static FishCatch CreateCatch(
        DateTimeOffset date,
        string name,
        FishRarity rarity,
        string? mutation,
        double weight) =>
        new(Guid.NewGuid(), date, name, rarity, mutation, weight, null, 1);
}
