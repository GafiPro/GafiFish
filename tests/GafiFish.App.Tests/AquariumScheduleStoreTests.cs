using GafiFish.App.Settings;
using GafiFish.Core.Aquarium;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class AquariumScheduleStoreTests
{
    [Fact]
    public async Task SaveAndLoadPreservesFoodAndClaimTimes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-aquarium-{Guid.NewGuid():N}.json");
        var fedAt = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        var claimedAt = fedAt.AddMinutes(15);
        var original = new AquariumScheduleState
        {
            GuppiesBest = new AquariumFoodProgress(fedAt, fedAt.AddMinutes(2)),
            FishChow = new AquariumFoodProgress(fedAt.AddMinutes(5), null),
            LastClaimedAt = claimedAt,
            ClaimReminderEnabled = true,
            ReadySoundEnabled = true
        };

        try
        {
            var store = new AquariumScheduleStore(path);
            await store.SaveAsync(original);
            var loaded = await store.LoadAsync();

            Assert.Equal(original, loaded);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
            if (File.Exists(path + ".tmp"))
                File.Delete(path + ".tmp");
        }
    }

    [Fact]
    public async Task InvalidJsonFallsBackToDefaultState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-aquarium-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, "{ invalid json");
            var loaded = await new AquariumScheduleStore(path).LoadAsync();

            Assert.Equal(new AquariumScheduleState(), loaded);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
