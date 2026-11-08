using GafiFish.App.Settings;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class RodProfileBindingStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsEmptyBindingsWhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-rod-bindings-{Guid.NewGuid():N}.json");
        var bindings = await new RodProfileBindingStore(path).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Empty(bindings.SlotProfiles);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripPreservesSlotMappings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-rod-bindings-{Guid.NewGuid():N}.json");
        var store = new RodProfileBindingStore(path);
        var expected = new RodProfileSlotBindings
        {
            SlotProfiles = new Dictionary<int, string>
            {
                [1] = "Rod Alpha",
                [0] = "Rod-Beta"
            },
            RodNameProfiles = new Dictionary<string, string>
            {
                ["ABYSSALTRIDENT"] = "Rod Alpha",
                ["CELESTIALSTAFF"] = "Rod-Beta"
            }
        };

        try
        {
            await store.SaveAsync(expected, TestContext.Current.CancellationToken);
            var actual = await store.LoadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected.SlotProfiles.OrderBy(pair => pair.Key),
                actual.SlotProfiles.OrderBy(pair => pair.Key));
            Assert.Equal(expected.RodNameProfiles.OrderBy(pair => pair.Key),
                actual.RodNameProfiles.OrderBy(pair => pair.Key));
        }
        finally
        {
            foreach (var file in new[] { path, path + ".tmp" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Validate_RejectsOutOfRangeSlot()
    {
        var bindings = new RodProfileSlotBindings
        {
            SlotProfiles = new Dictionary<int, string> { [10] = "Rod Alpha" }
        };
        Assert.Throws<ArgumentOutOfRangeException>(bindings.Validate);
    }

    [Fact]
    public void Validate_RejectsNamesThatCannotProduceAProfileFile()
    {
        var bindings = new RodProfileSlotBindings
        {
            SlotProfiles = new Dictionary<int, string> { [2] = "!!!" }
        };
        Assert.Throws<ArgumentException>(bindings.Validate);
    }

    [Fact]
    public void Validate_RejectsRodNameKeysThatAreNotNormalized()
    {
        var bindings = new RodProfileSlotBindings
        {
            RodNameProfiles = new Dictionary<string, string>
            {
                ["Abyssal Trident"] = "Rod Alpha"
            }
        };
        Assert.Throws<ArgumentException>(bindings.Validate);
    }

    [Fact]
    public async Task LoadAsync_UsesEmptyBindingsWhenJsonIsCorrupt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-rod-bindings-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ invalid json", TestContext.Current.CancellationToken);
        try
        {
            var bindings = await new RodProfileBindingStore(path).LoadAsync(TestContext.Current.CancellationToken);
            Assert.Empty(bindings.SlotProfiles);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
