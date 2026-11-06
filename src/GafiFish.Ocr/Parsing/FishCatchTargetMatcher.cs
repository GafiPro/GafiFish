namespace GafiFish.Ocr.Parsing;

/// <summary>
/// Matches a requested target against an OCR catch notification. Matching is
/// case-insensitive and ignores punctuation, but requires every target word
/// and an explicit "caught" signal so ordinary UI text cannot trigger a stop.
/// </summary>
public static class FishCatchTargetMatcher
{
    public static bool Matches(string? recognizedText, string? target)
    {
        if (string.IsNullOrWhiteSpace(recognizedText) || string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var sourceWords = Tokenize(recognizedText);
        var targetWords = Tokenize(target);
        if (sourceWords.Count == 0 || targetWords.Count == 0
            || !sourceWords.Contains("caught", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return targetWords.All(word => sourceWords.Contains(word, StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> Tokenize(string value)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(char.ToLowerInvariant(character));
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }
}
