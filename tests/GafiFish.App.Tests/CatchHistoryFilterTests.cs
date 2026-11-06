using GafiFish.App.Models;
using GafiFish.Core.Models;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class CatchHistoryFilterTests
{
    [Fact]
    public void Apply_SearchAndRarityFiltersAreCombined()
    {
        var rows = new[]
        {
            Create("Megalodon", FishRarity.Mythical, null, DateTimeOffset.Parse("2026-01-01T10:00:00Z")),
            Create("Megalodon", FishRarity.Legendary, null, DateTimeOffset.Parse("2026-01-01T11:00:00Z")),
            Create("Scylla", FishRarity.Mythical, null, DateTimeOffset.Parse("2026-01-01T12:00:00Z"))
        };

        var result = CatchHistoryFilter.Apply(rows, "mega", "Mythical", false, false);

        var item = Assert.Single(result);
        Assert.Equal("Megalodon", item.Species);
        Assert.Equal(FishRarity.Mythical, item.Rarity);
    }

    [Fact]
    public void Apply_UnknownFilterIncludesNullAndUnknownRarity()
    {
        var rows = new[]
        {
            Create("Unknown A", null, null, DateTimeOffset.Parse("2026-01-01T10:00:00Z")),
            Create("Unknown B", FishRarity.Unknown, null, DateTimeOffset.Parse("2026-01-01T11:00:00Z")),
            Create("Common fish", FishRarity.Common, null, DateTimeOffset.Parse("2026-01-01T12:00:00Z"))
        };

        var result = CatchHistoryFilter.Apply(rows, null, "Unknown", false, false);

        Assert.Equal(2, result.Count);
        Assert.All(result, row => Assert.True(row.Rarity is null or FishRarity.Unknown));
    }

    [Fact]
    public void Apply_IdentifiedAndMutationFiltersCanBeCombined()
    {
        var rows = new[]
        {
            Create("Wrath Scylla", FishRarity.Secret, "Shiny", DateTimeOffset.Parse("2026-01-01T10:00:00Z")),
            Create("Megalodon", FishRarity.Mythical, null, DateTimeOffset.Parse("2026-01-01T11:00:00Z")),
            Create(null, null, "Sparkling", DateTimeOffset.Parse("2026-01-01T12:00:00Z"))
        };

        var result = CatchHistoryFilter.Apply(rows, null, "All rarities", true, true);

        var item = Assert.Single(result);
        Assert.Equal("Wrath Scylla", item.Species);
    }

    [Fact]
    public void Apply_SearchCanFindSessionIdAndMutation()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var row = Create("Scylla", FishRarity.Secret, "Abyssal", DateTimeOffset.UtcNow, id);

        Assert.Single(CatchHistoryFilter.Apply(new[] { row }, "abyss", "All rarities", false, false));
        Assert.Single(CatchHistoryFilter.Apply(new[] { row }, "aaaaaaaa-bbbb", "All rarities", false, false));
    }

    [Fact]
    public void Apply_OrdersNewestFirst()
    {
        var older = Create("Older", FishRarity.Common, null, DateTimeOffset.Parse("2026-01-01T10:00:00Z"));
        var newer = Create("Newer", FishRarity.Common, null, DateTimeOffset.Parse("2026-01-01T12:00:00Z"));

        var result = CatchHistoryFilter.Apply(new[] { older, newer }, null, null, false, false);

        Assert.Equal(new[] { "Newer", "Older" }, result.Select(row => row.Species));
    }

    [Fact]
    public void Apply_RejectsNullSource()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CatchHistoryFilter.Apply(null!, null, null, false, false));
    }

    private static CatchHistoryRow Create(
        string? species,
        FishRarity? rarity,
        string? mutation,
        DateTimeOffset caughtAt,
        Guid? sessionId = null) =>
        new(
            sessionId ?? Guid.NewGuid(),
            DateTimeOffset.Parse("2026-01-01T09:00:00Z"),
            caughtAt,
            species,
            rarity,
            mutation,
            12.5,
            100m,
            0.95);
}
