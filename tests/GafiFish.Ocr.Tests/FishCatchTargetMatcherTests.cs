using GafiFish.Ocr.Parsing;
using Xunit;

namespace GafiFish.Ocr.Tests;

public sealed class FishCatchTargetMatcherTests
{
    [Theory]
    [InlineData("You caught a Wrath Scylla!", "Wrath Scylla", true)]
    [InlineData("YOU CAUGHT: WRATH-SCYLLA", "wrath scylla", true)]
    [InlineData("You caught Scylla with Wrath mutation", "Wrath Scylla", true)]
    [InlineData("You caught a Megalodon", "Wrath Scylla", false)]
    [InlineData("Wrath Scylla", "Wrath Scylla", false)]
    [InlineData("You caught a Scylla", "Wrath Scylla", false)]
    [InlineData("You caught a Wrath Scylla", "", false)]
    public void Matches_requires_catch_signal_and_all_target_words(
        string recognizedText,
        string target,
        bool expected)
    {
        Assert.Equal(expected, FishCatchTargetMatcher.Matches(recognizedText, target));
    }
}
