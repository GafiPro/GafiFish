using GafiFish.Core.Models;
using GafiFish.Data.Storage;
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

    private static void DeleteDatabaseFiles(string path)
    {
        foreach (var candidate in new[] { path, path + "-shm", path + "-wal" })
        {
            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
    }
}
