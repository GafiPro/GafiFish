using GafiFish.Ocr.Parsing;
using Xunit;

namespace GafiFish.Ocr.Tests;

public sealed class FishCatchTargetMatcherTests
{
    [Theory]
    [InlineData("Wrath Scylla", "Wrath Scylla", true)]
    [InlineData("WRATH-SCYLLA!", "wrath scylla", true)]
    [InlineData("Scylla with Wrath mutation", "Wrath Scylla", false)]
    [InlineData("Megalodon", "Wrath Scylla", false)]
    [InlineData("Scylla", "Wrath Scylla", false)]
    [InlineData("Wrath Scylla", "", false)]
    public void MatchesSpecies_uses_exact_normalized_species_name(
        string recognizedSpecies,
        string target,
        bool expected)
    {
        Assert.Equal(expected, FishCatchTargetMatcher.MatchesSpecies(recognizedSpecies, target));
    }

    [Theory]
    [InlineData("You caught a Wrath Scylla!", "Wrath Scylla", true)]
    [InlineData("YOU CAUGHT: WRATH-SCYLLA!", "wrath scylla", true)]
    [InlineData("You caught Scylla with Wrath mutation", "Wrath Scylla", false)]
    [InlineData("You caught a Megalodon", "Wrath Scylla", false)]
    [InlineData("Wrath Scylla", "Wrath Scylla", false)]
    public void Matches_uses_only_species_phrase_after_catch_signal(
        string recognizedText,
        string target,
        bool expected)
    {
        Assert.Equal(expected, FishCatchTargetMatcher.Matches(recognizedText, target));
    }
    [Fact]
    public void MatchesSpecies_RejectsNotificationCapturedBeforeRequestStart()
    {
        var startedAt = DateTimeOffset.UtcNow;
        Assert.False(FishCatchTargetMatcher.MatchesSpecies(
            "Wrath Scylla", "Wrath Scylla", startedAt.AddMilliseconds(-1), startedAt));
        Assert.True(FishCatchTargetMatcher.MatchesSpecies(
            "Wrath Scylla", "Wrath Scylla", startedAt.AddMilliseconds(1), startedAt));
    }

}
