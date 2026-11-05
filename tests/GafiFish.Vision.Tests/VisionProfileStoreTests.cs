using GafiFish.Vision.Calibration;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class VisionProfileStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsRegionAndMarkerColors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.json");
        var store = new VisionProfileStore(path);
        var profile = new VisionProfile
        {
            ReferenceWidth = 1920,
            ReferenceHeight = 1080,
            FishingRegion = new NormalizedRegion(0.20, 0.70, 0.60, 0.20),
            FishMarkerColor = new HsvColorRange(20, 100, 80, 40, 255, 255),
            ControlMarkerColor = new HsvColorRange(0, 0, 160, 10, 80, 255)
        };

        try
        {
            await store.SaveAsync(profile, TestContext.Current.CancellationToken);
            var loaded = await store.LoadOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.Equal(profile.ReferenceWidth, loaded.ReferenceWidth);
            Assert.Equal(profile.ReferenceHeight, loaded.ReferenceHeight);
            Assert.Equal(profile.FishingRegion, loaded.FishingRegion);
            Assert.Equal(profile.FishMarkerColor, loaded.FishMarkerColor);
            Assert.Equal(profile.ControlMarkerColor, loaded.ControlMarkerColor);
            Assert.True(loaded.IsFishingCalibrated);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task LoadOrDefault_ReturnsEmptyProfileForCorruptJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ not valid json", TestContext.Current.CancellationToken);

        try
        {
            var profile = await new VisionProfileStore(path).LoadOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.False(profile.IsFishingCalibrated);
            Assert.Equal(NormalizedRegion.FullFrame, profile.FishingRegion);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
