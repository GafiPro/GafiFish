using GafiFish.App.Settings;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class CatchAlertSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenFileDoesNotExist()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var settings = await new CatchAlertSettingsStore(
                Path.Combine(directory, "alerts.json")).LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(string.Empty, settings.WatchedSpecies);
            Assert.True(settings.AlertOnLegendaryOrHigher);
            Assert.False(settings.AlertOnMutation);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripPreservesAlertPreferences()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "alerts.json");
        var expected = new CatchAlertSettings
        {
            WatchedSpecies = "Scylla, Megalodon; Bluefin Tuna",
            AlertOnLegendaryOrHigher = false,
            AlertOnMutation = true
        };

        try
        {
            var store = new CatchAlertSettingsStore(path);
            await store.SaveAsync(expected, TestContext.Current.CancellationToken);
            Assert.Equal(expected, await store.LoadAsync(TestContext.Current.CancellationToken));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenJsonIsInvalid()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "alerts.json");
        try
        {
            await File.WriteAllTextAsync(path, "{ not valid json");
            var settings = await new CatchAlertSettingsStore(path).LoadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(new CatchAlertSettings(), settings);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsOversizedSpeciesList()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new CatchAlertSettingsStore(Path.Combine(directory, "alerts.json"));
            var settings = new CatchAlertSettings { WatchedSpecies = new string('x', 2001) };
            await Assert.ThrowsAsync<ArgumentException>(() =>
                store.SaveAsync(settings, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gafifish-alerts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
