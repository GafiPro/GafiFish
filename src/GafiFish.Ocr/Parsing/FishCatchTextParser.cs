using System.Globalization;
using System.Text.RegularExpressions;
using GafiFish.Core.Models;
using GafiFish.Ocr.Models;

namespace GafiFish.Ocr.Parsing;

/// <summary>
/// Conservative English notification parser. It only creates a catch when a
/// catch verb and an identifiable species phrase are present; uncertain fields
/// remain null rather than being guessed.
/// </summary>
public sealed partial class FishCatchTextParser
{
    private static readonly Regex CatchSignalRegex = new(
        @"\b(?:you\s+)?caught\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SpeciesRegex = new(
        @"\b(?:you\s+)?caught\s+(?:a|an|the)?\s*(?<species>[\p{L}\p{N}][\p{L}\p{N} '\u2019-]{0,48}?)(?=\s*(?:[!.,;\r\n]|\bweigh(?:ing|t)?\b|\bworth\b|\bwith\s+[\p{L}\p{N}][\p{L}\p{N} '\u2019-]{0,24}?\s+mutation\b|\bmutation\b|\$|$))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WeightRegex = new(
        @"\b(?<weight>\d+(?:[.,]\d+)?)\s*(?:kg|kgs|kilograms?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ValueRegex = new(
        @"\$\s*(?<value>\d[\d,]*(?:\.\d{1,2})?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly (Regex Pattern, FishRarity Rarity)[] RarityPatterns =
    [
        (new Regex(@"\bapex\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), FishRarity.Apex),
        (new Regex(@"\bsecret\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), FishRarity.Secret),
        (new Regex(@"\bexotic\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), FishRarity.Exotic),
        (new Regex(@"\bmythical\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), FishRarity.Mythical),
        (new Regex(@"\blegendary\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), FishRarity.Legendary),
        (new Regex(@"\brare\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), FishRarity.Rare),
        (new Regex(@"\bunusual\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), FishRarity.Unusual),
        (new Regex(@"\bcommon\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), FishRarity.Common)
    ];

    private static readonly string[] GenericSpecies =
    [
        "fish", "a fish", "an fish", "something", "it"
    ];

    public double MinimumConfidence { get; init; } = 0.45;

    public bool TryParse(
        LocalOcrResult result,
        out FishCatch? fishCatch,
        out string status,
        DateTimeOffset? caughtAt = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        fishCatch = null;

        if (!double.IsFinite(result.Confidence)
            || result.Confidence < MinimumConfidence)
        {
            status = "OCR confidence is too low to register a catch.";
            return false;
        }

        var text = Normalize(result.Text);
        if (text.Length == 0 || !CatchSignalRegex.IsMatch(text))
        {
            status = "No fish-catch notification detected.";
            return false;
        }

        var speciesMatch = SpeciesRegex.Match(text);
        if (!speciesMatch.Success)
        {
            status = "A catch notification was seen, but the species could not be identified.";
            return false;
        }

        var species = CleanSpecies(speciesMatch.Groups["species"].Value);
        if (species.Length < 2 || GenericSpecies.Contains(species, StringComparer.OrdinalIgnoreCase))
        {
            status = "A catch notification was seen, but the species text is too generic.";
            return false;
        }

        var rarity = ParseRarity(text);
        var mutation = ParseMutation(text);
        var weight = ParseWeight(text);
        var value = ParseValue(text);

        fishCatch = new FishCatch(
            Guid.NewGuid(),
            caughtAt ?? result.CapturedAt,
            species,
            rarity,
            mutation,
            weight,
            value,
            Math.Clamp(result.Confidence, 0, 1));

        status = $"Catch recognised: {species}" +
            (rarity.HasValue ? $" · {rarity.Value}" : string.Empty) +
            (weight.HasValue ? $" · {weight.Value.ToString("0.###", CultureInfo.InvariantCulture)} kg" : string.Empty);
        return true;
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text
            .Replace('\u00A0', ' ')
            .Replace('\u2019', '\'')
            .Trim();
    }

    private static string CleanSpecies(string species)
    {
        var cleaned = Regex.Replace(species, @"\s+", " ").Trim();
        cleaned = cleaned.Trim(' ', '\'', '"', '-', ':');
        return cleaned;
    }

    private static FishRarity? ParseRarity(string text)
    {
        foreach (var (pattern, rarity) in RarityPatterns)
        {
            if (pattern.IsMatch(text))
            {
                return rarity;
            }
        }

        return null;
    }

    private static string? ParseMutation(string text)
    {
        var explicitMutation = Regex.Match(
            text,
            @"\bwith\s+(?<mutation>[\p{L}\p{N}][\p{L}\p{N} '\u2019-]{0,24}?)\s+mutation\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (explicitMutation.Success)
        {
            var value = Regex.Replace(explicitMutation.Groups["mutation"].Value, @"\s+", " ").Trim();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }

        var mutation = Regex.Match(
            text,
            @"\b(shiny|sparkling|golden|albino|darkened)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return mutation.Success
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(mutation.Value.ToLowerInvariant())
            : null;
    }

    private static double? ParseWeight(string text)
    {
        var match = WeightRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var normalized = match.Groups["weight"].Value.Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var weight) && double.IsFinite(weight) && weight > 0 && weight <= 100_000
                ? weight
                : null;
    }

    private static decimal? ParseValue(string text)
    {
        var match = ValueRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var normalized = match.Groups["value"].Value.Replace(",", string.Empty);
        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var value) && value >= 0
                ? value
                : null;
    }
}
