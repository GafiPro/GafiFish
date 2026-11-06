using GafiFish.Core.Models;
using GafiFish.Core.Services;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class CatchAlertMatcherTests
{
    [Fact]
    public void MatchesPrioritySpeciesCaseInsensitivelyUsingPartialNames()
    {
        var fish = Catch("Wrath Scylla", FishRarity.Common, null);
        var options = new CatchAlertOptions(["scylla"], false, false);

        var reasons = CatchAlertMatcher.GetReasons(fish, options);

        Assert.Single(reasons);
        Assert.Contains("Priority species", reasons[0]);
    }

    [Theory]
    [InlineData(FishRarity.Legendary)]
    [InlineData(FishRarity.Mythical)]
    [InlineData(FishRarity.Exotic)]
    [InlineData(FishRarity.Secret)]
    [InlineData(FishRarity.Apex)]
    public void AlertsForLegendaryAndHigher(FishRarity rarity)
    {
        var reasons = CatchAlertMatcher.GetReasons(
            Catch("Test Fish", rarity, null),
            new CatchAlertOptions([], true, false));

        Assert.Single(reasons);
        Assert.Contains(rarity.ToString(), reasons[0]);
    }

    [Fact]
    public void DoesNotAlertForCommonFishWhenOnlyHighRarityIsEnabled()
    {
        var reasons = CatchAlertMatcher.GetReasons(
            Catch("Test Fish", FishRarity.Common, null),
            new CatchAlertOptions([], true, false));

        Assert.Empty(reasons);
    }

    [Fact]
    public void AlertsForMutationOnlyWhenEnabled()
    {
        var fish = Catch("Golden Koi", FishRarity.Common, "Golden");

        Assert.Empty(CatchAlertMatcher.GetReasons(fish, new CatchAlertOptions([], false, false)));
        Assert.Single(CatchAlertMatcher.GetReasons(fish, new CatchAlertOptions([], false, true)));
    }

    [Fact]
    public void UnknownSpeciesDoesNotMatchWatchList()
    {
        var reasons = CatchAlertMatcher.GetReasons(
            Catch(null, FishRarity.Common, null),
            new CatchAlertOptions(["Scylla"], false, false));

        Assert.Empty(reasons);
    }

    private static FishCatch Catch(string? species, FishRarity rarity, string? mutation) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, species, rarity, mutation, 1.25, 100m, 0.97);
}
