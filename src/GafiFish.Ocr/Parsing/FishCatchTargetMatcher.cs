using System.Text.RegularExpressions;

namespace GafiFish.Ocr.Parsing;

/// <summary>
/// Matches the parsed species field, not all words in the notification. This
/// prevents a mutation name elsewhere in the message from being mistaken for
/// part of the fish species and stopping a target-fish session incorrectly.
/// </summary>
public static class FishCatchTargetMatcher
{
    public static bool MatchesSpecies(string? recognizedSpecies, string? target)
    {
        if (string.IsNullOrWhiteSpace(recognizedSpecies) || string.IsNullOrWhiteSpace(target))
            return false;

        return Normalize(recognizedSpecies) == Normalize(target);
    }

    /// <summary>
    /// Convenience parser for an unparsed English notification. The species
    /// text must immediately follow the catch signal; unrelated later words
    /// cannot complete a partial target match.
    /// </summary>
    public static bool Matches(string? recognizedText, string? target)
    {
        if (string.IsNullOrWhiteSpace(recognizedText) || string.IsNullOrWhiteSpace(target))
            return false;

        var match = Regex.Match(
            recognizedText,
            @"\b(?:you\s+)?caught\s+(?:a|an|the)?\s*(?<species>[^!.,;\r\n]+?)(?=\s*(?:[!.,;]|\bweigh(?:ing|t)?\b|\bworth\b|\bwith\s+[\p{L}\p{N}][\p{L}\p{N} '\u2019-]{0,24}?\s+mutation\b|\bmutation\b|\$|$))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && MatchesSpecies(match.Groups["species"].Value, target);
    }

    private static string Normalize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
