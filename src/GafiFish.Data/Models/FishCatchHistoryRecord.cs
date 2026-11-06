namespace GafiFish.Data.Models;

public sealed record FishCatchHistoryRecord(
    Guid SessionId,
    DateTimeOffset SessionStartedAt,
    DateTimeOffset? SessionEndedAt,
    GafiFish.Core.Models.FishCatch Catch);
