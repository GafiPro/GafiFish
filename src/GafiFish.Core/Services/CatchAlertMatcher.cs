using System.Globalization;
using GafiFish.Core.Models;

namespace GafiFish.Core.Services;

public static class CatchAlertMatcher
{
    public static IReadOnlyList<string> GetReasons(FishCatch fishCatch, CatchAlertOptions options)
    {
        ArgumentNullException.ThrowIfNull(fishCatch);
        ArgumentNullException.ThrowIfNull(options);

        var reasons = new List<string>();
        var species = fishCatch.Species?.Trim();

        if (!string.IsNullOrWhiteSpace(species)
            && options.WatchedSpecies.Any(token =>
                !string.IsNullOrWhiteSpace(token)
                && species.Contains(token.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add($"Priority species: {species}");
        }

        if (options.AlertOnLegendaryOrHigher
            && fishCatch.Rarity is FishRarity.Legendary
                or FishRarity.Mythical
                or FishRarity.Exotic
                or FishRarity.Secret
                or FishRarity.Apex)
        {
            reasons.Add($"High rarity: {fishCatch.Rarity}");
        }

        if (options.AlertOnMutation && !string.IsNullOrWhiteSpace(fishCatch.Mutation))
        {
            reasons.Add($"Mutation: {fishCatch.Mutation}");
        }

        if (options.MinimumWeightKg is double minimumWeight
            && double.IsFinite(minimumWeight)
            && minimumWeight > 0
            && fishCatch.WeightKg is double actualWeight
            && double.IsFinite(actualWeight)
            && actualWeight >= minimumWeight)
        {
            reasons.Add($"Weight threshold: {minimumWeight.ToString("0.##", CultureInfo.InvariantCulture)} kg+");
        }

        if (options.MinimumEstimatedValue is decimal minimumValue
            && minimumValue > 0
            && fishCatch.EstimatedValue is decimal actualValue
            && actualValue >= minimumValue)
        {
            reasons.Add($"Estimated value threshold: {minimumValue.ToString("0.##", CultureInfo.InvariantCulture)}+");
        }

        return reasons;
    }
}
