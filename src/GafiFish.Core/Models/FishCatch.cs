namespace GafiFish.Core.Models;

/// <summary>
/// A locally recognised catch. Nullable fields are intentional: OCR must never
/// invent a species, rarity, mutation, weight, or value when recognition fails.
/// </summary>
public sealed record FishCatch(
    Guid Id,
    DateTimeOffset CaughtAt,
    string? Species,
    FishRarity? Rarity,
    string? Mutation,
    double? WeightKg,
    decimal? EstimatedValue,
    double RecognitionConfidence)
{
    public bool IsIdentified => !string.IsNullOrWhiteSpace(Species);
}
