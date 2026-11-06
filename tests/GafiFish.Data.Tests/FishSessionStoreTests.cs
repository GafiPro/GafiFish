using GafiFish.Core.Models;
using GafiFish.Data.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GafiFish.Data.Tests;

public sealed class FishSessionStoreTests
{
    [Fact]
    public async Task SessionAndCatches_PersistAndReturnTop25Summary()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.db");
        var store = new FishSessionStore(path);
        var startedAt = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        try
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            var sessionId = await store.StartSessionAsync(
                startedAt, TestContext.Current.CancellationToken);

            for (var i = 0; i < 30; i++)
            {
                var fish = new FishCatch(
                    Guid.NewGuid(),
                    startedAt.AddMinutes(i),
                    $"Fish {i}",
                    i == 29 ? FishRarity.Secret : FishRarity.Common,
                    i == 29 ? "Shiny" : null,
                    1 + i,
                    100 + i,
                    0.99);

                await store.SaveCatchAsync(
                    sessionId, fish, TestContext.Current.CancellationToken);
            }

            var endedAt = startedAt.AddMinutes(35);
            await store.FinishSessionAsync(
                sessionId, endedAt, TestContext.Current.CancellationToken);

            var session = await store.GetSessionAsync(
                sessionId, TestContext.Current.CancellationToken);
            var catches = await store.GetCatchesAsync(
                sessionId, TestContext.Current.CancellationToken);
            var summary = await store.GetSessionSummaryAsync(
                sessionId, cancellationToken: TestContext.Current.CancellationToken);
            var recent = await store.GetRecentSessionsAsync(
                cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(session);
            Assert.Equal(30, session.TotalCatches);
            Assert.Equal(endedAt, session.EndedAt);
            Assert.Equal(30, catches.Count);
            Assert.NotNull(summary);
            Assert.Equal(30, summary.TotalCatches);
            Assert.Equal(25, summary.TopCatches.Count);
            Assert.True(summary.WasTruncated);
            Assert.Equal("Fish 29", summary.TopCatches[0].Species);
            Assert.Single(recent);
            Assert.Equal(sessionId, recent[0].Id);
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task SaveCatch_RejectsEndedSession()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.db");
        var store = new FishSessionStore(path);
        var now = DateTimeOffset.UtcNow;

        try
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            var sessionId = await store.StartSessionAsync(now, TestContext.Current.CancellationToken);
            await store.FinishSessionAsync(sessionId, now.AddMinutes(1), TestContext.Current.CancellationToken);

            var fish = new FishCatch(
                Guid.NewGuid(), now, "Test", FishRarity.Common, null, 1, 1, 1);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.SaveCatchAsync(sessionId, fish, TestContext.Current.CancellationToken));
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    [Fact]
    public async Task GetLifetimeStatistics_AggregatesSessionsCatchesAndTopSpecies()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.db");
        var store = new FishSessionStore(path);
        var startedAt = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        try
        {
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            var firstSession = await store.StartSessionAsync(startedAt, TestContext.Current.CancellationToken);
            await store.SaveCatchAsync(firstSession,
                new FishCatch(Guid.NewGuid(), startedAt.AddMinutes(5), "Golden Koi", FishRarity.Secret, "Golden", 5.0, 1200m, 0.98),
                TestContext.Current.CancellationToken);
            await store.SaveCatchAsync(firstSession,
                new FishCatch(Guid.NewGuid(), startedAt.AddMinutes(15), "Golden Koi", FishRarity.Common, null, 2.0, 100m, 0.96),
                TestContext.Current.CancellationToken);
            await store.FinishSessionAsync(firstSession, startedAt.AddHours(1), TestContext.Current.CancellationToken);

            var secondSession = await store.StartSessionAsync(startedAt.AddDays(1), TestContext.Current.CancellationToken);
            await store.SaveCatchAsync(secondSession,
                new FishCatch(Guid.NewGuid(), startedAt.AddDays(1).AddMinutes(10), "Bluefin Tuna", FishRarity.Legendary, "Shiny", 10.0, 2500m, 0.99),
                TestContext.Current.CancellationToken);
            await store.FinishSessionAsync(secondSession, startedAt.AddDays(1).AddMinutes(30), TestContext.Current.CancellationToken);

            var statistics = await store.GetLifetimeStatisticsAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, statistics.TotalSessions);
            Assert.Equal(3, statistics.TotalCatches);
            Assert.Equal(3, statistics.IdentifiedCatches);
            Assert.Equal(2, statistics.MutatedCatches);
            Assert.Equal(2, statistics.LegendaryOrHigherCatches);
            Assert.Equal(3800m, statistics.TotalEstimatedValue);
            Assert.NotNull(statistics.AverageWeightKg);
            Assert.Equal(17.0 / 3.0, statistics.AverageWeightKg.Value, precision: 8);
            Assert.Equal(10.0, statistics.HeaviestWeightKg);
            Assert.Equal(1.5, statistics.CompletedFishingHours, precision: 6);
            Assert.Equal(2.0, statistics.CatchesPerHour, precision: 6);
            Assert.Equal("Golden Koi", statistics.TopSpecies[0].Species);
            Assert.Equal(2, statistics.TopSpecies[0].Count);
        }
        finally
        {
            DeleteDatabaseFiles(path);
        }
    }

    private static void DeleteDatabaseFiles(string path)
    {
        SqliteConnection.ClearAllPools();

        foreach (var candidate in new[] { path, path + "-shm", path + "-wal" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
