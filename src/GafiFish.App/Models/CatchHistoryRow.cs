using System.Globalization;
using GafiFish.Core.Models;

namespace GafiFish.App.Models;

/// <summary>
/// Presentation-friendly catch data for the searchable local archive.
/// </summary>
public sealed record CatchHistoryRow(
    Guid SessionId,
    DateTimeOffset SessionStartedAt,
    DateTimeOffset CaughtAt,
    string? Species,
    FishRarity? Rarity,
    string? Mutation,
    double? WeightKg,
    decimal? EstimatedValue,
    double RecognitionConfidence)
{
    public bool IsIdentified => !string.IsNullOrWhiteSpace(Species);
    public bool HasMutation => !string.IsNullOrWhiteSpace(Mutation);

    public string SpeciesDisplay => IsIdentified ? Species! : "Unidentified";
    public string RarityDisplay => Rarity?.ToString() ?? "Unknown";
    public string MutationDisplay => HasMutation ? Mutation! : "—";
    public string CaughtAtDisplay => CaughtAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string SessionStartedAtDisplay => SessionStartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string WeightDisplay => WeightKg.HasValue
        ? $"{WeightKg.Value.ToString("F2", CultureInfo.CurrentCulture)} kg"
        : "—";
    public string EstimatedValueDisplay => EstimatedValue.HasValue
        ? EstimatedValue.Value.ToString("N0", CultureInfo.CurrentCulture)
        : "—";
    public string ConfidenceDisplay => RecognitionConfidence.ToString("P0", CultureInfo.CurrentCulture);

    public string SearchableText => string.Join(" ",
        SpeciesDisplay,
        RarityDisplay,
        MutationDisplay,
        WeightKg?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        EstimatedValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        SessionId.ToString("D"));
}

public static class CatchHistoryFilter
{
    public static IReadOnlyList<CatchHistoryRow> Apply(
        IEnumerable<CatchHistoryRow> source,
        string? searchText,
        string? rarityFilter,
        bool identifiedOnly,
        bool mutatedOnly)
    {
        ArgumentNullException.ThrowIfNull(source);

        var search = searchText?.Trim() ?? string.Empty;
        var rarity = string.IsNullOrWhiteSpace(rarityFilter) ? "All rarities" : rarityFilter.Trim();

        return source
            .Where(item =>
                (search.Length == 0 || item.SearchableText.Contains(search, StringComparison.OrdinalIgnoreCase))
                && (!identifiedOnly || item.IsIdentified)
                && (!mutatedOnly || item.HasMutation)
                && (rarity.Equals("All rarities", StringComparison.OrdinalIgnoreCase)
                    || (rarity.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                        ? item.Rarity is null or FishRarity.Unknown
                        : string.Equals(item.Rarity?.ToString(), rarity, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(item => item.CaughtAt)
            .ThenBy(item => item.Species, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
