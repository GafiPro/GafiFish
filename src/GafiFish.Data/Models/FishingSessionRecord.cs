namespace GafiFish.Data.Models;

public sealed record FishingSessionRecord(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int TotalCatches);
