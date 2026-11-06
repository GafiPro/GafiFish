namespace GafiFish.Core.Aquarium;

public enum AquariumFoodType
{
    GuppiesBest,
    FishChow,
    SplashSnacks
}

public sealed record AquariumFoodProgress(
    DateTimeOffset? LastFedAt = null,
    DateTimeOffset? LastRestockedAt = null);

public sealed record AquariumScheduleState
{
    public AquariumFoodProgress GuppiesBest { get; init; } = new();
    public AquariumFoodProgress FishChow { get; init; } = new();
    public AquariumFoodProgress SplashSnacks { get; init; } = new();
    public DateTimeOffset? LastClaimedAt { get; init; }
    public bool ClaimReminderEnabled { get; init; }
    public bool ReadySoundEnabled { get; init; }
    public bool AutoFeedEnabled { get; init; }
    public bool AutoRestockEnabled { get; init; }
    public bool AutoClaimEnabled { get; init; }

    public AquariumFoodProgress GetProgress(AquariumFoodType foodType) =>
        foodType switch
        {
            AquariumFoodType.GuppiesBest => GuppiesBest,
            AquariumFoodType.FishChow => FishChow,
            AquariumFoodType.SplashSnacks => SplashSnacks,
            _ => throw new ArgumentOutOfRangeException(nameof(foodType))
        };

    public AquariumScheduleState MarkFed(AquariumFoodType foodType, DateTimeOffset completedAt) =>
        UpdateFood(foodType, progress => progress with { LastFedAt = completedAt });

    public AquariumScheduleState MarkRestocked(AquariumFoodType foodType, DateTimeOffset completedAt) =>
        UpdateFood(foodType, progress => progress with { LastRestockedAt = completedAt });

    public AquariumScheduleState MarkClaimed(DateTimeOffset completedAt) =>
        this with { LastClaimedAt = completedAt };

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(GuppiesBest);
        ArgumentNullException.ThrowIfNull(FishChow);
        ArgumentNullException.ThrowIfNull(SplashSnacks);
    }

    private AquariumScheduleState UpdateFood(
        AquariumFoodType foodType,
        Func<AquariumFoodProgress, AquariumFoodProgress> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var current = GetProgress(foodType);
        var updated = update(current);
        return foodType switch
        {
            AquariumFoodType.GuppiesBest => this with { GuppiesBest = updated },
            AquariumFoodType.FishChow => this with { FishChow = updated },
            AquariumFoodType.SplashSnacks => this with { SplashSnacks = updated },
            _ => throw new ArgumentOutOfRangeException(nameof(foodType))
        };
    }
}
