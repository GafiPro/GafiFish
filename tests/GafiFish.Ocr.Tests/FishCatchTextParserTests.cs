using GafiFish.Core.Models;
using GafiFish.Ocr.Models;
using GafiFish.Ocr.Parsing;
using GafiFish.Vision.Calibration;
using Xunit;

namespace GafiFish.Ocr.Tests;

public sealed class FishCatchTextParserTests
{
    private readonly FishCatchTextParser _parser = new();

    [Fact]
    public void TryParse_ExtractsSpeciesRarityMutationWeightAndValue()
    {
        var timestamp = new DateTimeOffset(2026, 11, 5, 20, 0, 0, TimeSpan.Zero);
        var result = Result(
            "You caught a Golden Koi! Secret · 5.2 kg · $1,200",
            confidence: 0.97,
            timestamp);

        var success = _parser.TryParse(result, out var fish, out var status);

        Assert.True(success, status);
        Assert.NotNull(fish);
        Assert.Equal("Golden Koi", fish.Species);
        Assert.Equal(FishRarity.Secret, fish.Rarity);
        Assert.Equal("Golden", fish.Mutation);
        Assert.Equal(5.2, fish.WeightKg);
        Assert.Equal(1200m, fish.EstimatedValue);
        Assert.Equal(timestamp, fish.CaughtAt);
    }

    [Fact]
    public void TryParse_ParsesCommaDecimalWeight()
    {
        var result = Result("You caught a Sea Bass! 1,25 kg", 0.91);

        var success = _parser.TryParse(result, out var fish, out var status);

        Assert.True(success, status);
        Assert.NotNull(fish);
        Assert.Equal(1.25, fish.WeightKg);
    }

    [Fact]
    public void TryParse_DoesNotRegisterTextWithoutCatchSignal()
    {
        var result = Result("A Legendary Golden Koi is nearby.", 0.99);

        var success = _parser.TryParse(result, out var fish, out var status);

        Assert.False(success);
        Assert.Null(fish);
        Assert.Contains("No fish-catch", status);
    }

    [Fact]
    public void TryParse_DoesNotGuessGenericSpecies()
    {
        var result = Result("You caught a fish!", 0.98);

        var success = _parser.TryParse(result, out var fish, out var status);

        Assert.False(success);
        Assert.Null(fish);
        Assert.Contains("generic", status);
    }

    [Fact]
    public void TryParse_RejectsLowConfidenceText()
    {
        var result = Result("You caught a Golden Koi!", 0.20);

        var success = _parser.TryParse(result, out var fish, out var status);

        Assert.False(success);
        Assert.Null(fish);
        Assert.Contains("confidence", status);
    }

    [Fact]
    public void TryParse_LeavesUnknownFieldsNullInsteadOfGuessing()
    {
        var result = Result("You caught a Bluefin Tuna!", 0.92);

        var success = _parser.TryParse(result, out var fish, out var status);

        Assert.True(success, status);
        Assert.NotNull(fish);
        Assert.Equal("Bluefin Tuna", fish.Species);
        Assert.Null(fish.Rarity);
        Assert.Null(fish.Mutation);
        Assert.Null(fish.WeightKg);
        Assert.Null(fish.EstimatedValue);
    }

    private static LocalOcrResult Result(
        string text,
        double confidence,
        DateTimeOffset? capturedAt = null) =>
        new(
            text,
            confidence,
            NormalizedRegion.FullFrame,
            capturedAt ?? new DateTimeOffset(2026, 11, 5, 20, 0, 0, TimeSpan.Zero));
}
