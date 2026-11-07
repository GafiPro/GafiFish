using GafiFish.App.Settings;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class RodNameMatcherTests
{
    [Theory]
    [InlineData("Abyssal Trident", "ABYSSALTRIDENT")]
    [InlineData("  celestial-staff  ", "CELESTIALSTAFF")]
    [InlineData("Rod 123!", "ROD123")]
    public void NormalizeName_RemovesSpacingAndPunctuation(string input, string expected)
    {
        Assert.Equal(expected, RodNameMatcher.NormalizeName(input));
    }

    [Fact]
    public void CleanCandidate_RemovesRodLabelPrefix()
    {
        Assert.Equal("Abyssal Trident", RodNameMatcher.CleanCandidate("Rod: Abyssal Trident\r\nOther HUD text"));
    }

    [Fact]
    public void CleanCandidate_ReturnsEmptyForBlankOcr()
    {
        Assert.Equal(string.Empty, RodNameMatcher.CleanCandidate(" \r\n "));
    }
}
