namespace GafiFish.App.Settings;

/// <summary>
/// Normalizes OCR text so rod names can be mapped consistently despite
/// punctuation, spaces, or letter-case differences.
/// </summary>
public static class RodNameMatcher
{
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    }

    public static string CleanCandidate(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return string.Empty;

        var candidate = ocrText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? string.Empty;

        foreach (var prefix in new[] { "Fishing Rod:", "Fishing Rod -", "Rod:", "Rod -" })
        {
            if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate[prefix.Length..].Trim();
                break;
            }
        }

        return candidate.Length <= 64 ? candidate : candidate[..64].Trim();
    }
}
