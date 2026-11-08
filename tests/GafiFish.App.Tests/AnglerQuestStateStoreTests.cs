using System.IO;
using System.Text.Json;
using GafiFish.App.Settings;
using GafiFish.Core.Automation;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class AnglerQuestStateStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenFileIsMissing_ReturnsEmptyState()
    {
        using var directory = new TemporaryDirectory();
        var store = new AnglerQuestStateStore(Path.Combine(directory.Path, "angler.json"));
        Assert.Equal(AnglerQuestSnapshot.Empty, await store.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAndLoad_PreservesSubmittedCooldown()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "angler.json");
        var store = new AnglerQuestStateStore(path);
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var tracker = new AnglerQuestTracker();
        tracker.StartTarget("Ancient Kraken", start);
        tracker.MarkTargetFishCaught("Ancient Kraken", start.AddMinutes(1));
        tracker.ConfirmQuestSubmitted(start.AddMinutes(2));
        store.Save(tracker.CreateSnapshot());
        var restored = await store.LoadAsync();
        Assert.Equal(tracker.CreateSnapshot(), restored);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task LoadAsync_RejectsCorruptJsonInsteadOfSilentlyClearingCooldown()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "angler.json");
        await File.WriteAllTextAsync(path, "{ this is not json }", TestContext.Current.CancellationToken);
        var store = new AnglerQuestStateStore(path);
        await Assert.ThrowsAsync<JsonException>(() => store.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_RejectsInconsistentCooldownSnapshot()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "angler.json");
        var caughtAt = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
        var invalid = new AnglerQuestSnapshot("Megalodon", caughtAt.AddMinutes(-1), caughtAt, caughtAt.AddSeconds(5), caughtAt.AddSeconds(100));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(invalid), TestContext.Current.CancellationToken);
        var store = new AnglerQuestStateStore(path);
        await Assert.ThrowsAsync<ArgumentException>(() => store.LoadAsync());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "GafiFish-AnglerTests-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
