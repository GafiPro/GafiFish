using GafiFish.App.Settings;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class AppHotkeySettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-settings-{Guid.NewGuid():N}.json");
        var settings = await new AppHotkeySettingsStore(path).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("F6", settings.CaptureKey);
        Assert.Equal("F7", settings.ObserveKey);
        Assert.Equal("F8", settings.StartKey);
        Assert.Equal("F9", settings.StopKey);
        Assert.Equal("F10", settings.ToggleUiKey);
        Assert.True(settings.HideUiWhileRunning);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripPreservesAllPreferences()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-settings-{Guid.NewGuid():N}.json");
        var store = new AppHotkeySettingsStore(path);
        var expected = new AppHotkeySettings
        {
            CaptureKey = "F2", ObserveKey = "F3", StartKey = "F4",
            StopKey = "F5", ToggleUiKey = "F11", HideUiWhileRunning = false
        };
        try
        {
            await store.SaveAsync(expected, TestContext.Current.CancellationToken);
            Assert.Equal(expected, await store.LoadAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            foreach (var file in new[] { path, path + ".tmp" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Validate_RejectsDuplicateKeys()
    {
        var settings = new AppHotkeySettings { CaptureKey = "F5", ObserveKey = "F5" };
        Assert.Throws<ArgumentException>(settings.Validate);
    }

    [Fact]
    public void Validate_RejectsUnsupportedKeys()
    {
        var settings = new AppHotkeySettings { CaptureKey = "A" };
        Assert.Throws<ArgumentException>(settings.Validate);
    }
}
