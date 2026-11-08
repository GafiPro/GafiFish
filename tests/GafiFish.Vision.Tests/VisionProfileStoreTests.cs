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
            FishingRegionCalibrated = true,
            RodMinigameMode = GafiFish.Core.Control.RodMinigameMode.DualFish,
            JinglestarHoldMilliseconds = 140,
            JinglestarReleaseMilliseconds = 90,
            JinglestarTimingCalibrated = true,
            RodChargeReleaseThreshold = 0.90,
            FishMarkerColor = new HsvColorRange(20, 100, 80, 40, 255, 255),
            SecondaryFishMarkerColor = new HsvColorRange(50, 100, 80, 70, 255, 255),
            ControlMarkerColor = new HsvColorRange(0, 0, 160, 10, 80, 255),
            ReconnectButtonRegion = new NormalizedRegion(0.35, 0.25, 0.20, 0.08),
            ReconnectButtonRegionCalibrated = true,
            ReconnectButtonColor = new HsvColorRange(100, 80, 100, 120, 255, 255),
            SessionIndicatorRegion = new NormalizedRegion(0.05, 0.55, 0.20, 0.10),
            SessionIndicatorRegionCalibrated = true,
            SessionIndicatorColor = new HsvColorRange(40, 80, 100, 70, 255, 255),
            WorldPhaseRegion = new NormalizedRegion(0.70, 0.10, 0.12, 0.18),
            WorldPhaseRegionCalibrated = true,
            DayPhaseColor = new HsvColorRange(0, 180, 180, 5, 255, 255),
            NightPhaseColor = new HsvColorRange(110, 180, 180, 130, 255, 255),
            FishMarkerTemplatePng = CreatePngTemplate(),
            ControlMarkerTemplatePng = CreatePngTemplate()
        };

        try
        {
            await store.SaveAsync(profile, TestContext.Current.CancellationToken);
            var loaded = await store.LoadOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.Equal(profile.ReferenceWidth, loaded.ReferenceWidth);
            Assert.Equal(profile.ReferenceHeight, loaded.ReferenceHeight);
            Assert.Equal(profile.FishingRegion, loaded.FishingRegion);
            Assert.Equal(profile.FishMarkerColor, loaded.FishMarkerColor);
            Assert.Equal(profile.SecondaryFishMarkerColor, loaded.SecondaryFishMarkerColor);
            Assert.Equal(profile.RodMinigameMode, loaded.RodMinigameMode);
            Assert.Equal(140, loaded.JinglestarHoldMilliseconds);
            Assert.Equal(90, loaded.JinglestarReleaseMilliseconds);
            Assert.True(loaded.JinglestarTimingCalibrated);
            Assert.Equal(0.90, loaded.RodChargeReleaseThreshold);
            Assert.True(loaded.IsDualFishCalibrated);
            Assert.Equal(profile.ControlMarkerColor, loaded.ControlMarkerColor);
            Assert.Equal(profile.ReconnectButtonRegion, loaded.ReconnectButtonRegion);
            Assert.Equal(profile.ReconnectButtonColor, loaded.ReconnectButtonColor);
            Assert.True(loaded.IsReconnectButtonCalibrated);
            Assert.Equal(profile.SessionIndicatorRegion, loaded.SessionIndicatorRegion);
            Assert.Equal(profile.SessionIndicatorColor, loaded.SessionIndicatorColor);
            Assert.True(loaded.IsSessionIndicatorCalibrated);
            Assert.Equal(profile.WorldPhaseRegion, loaded.WorldPhaseRegion);
            Assert.Equal(profile.DayPhaseColor, loaded.DayPhaseColor);
            Assert.Equal(profile.NightPhaseColor, loaded.NightPhaseColor);
            Assert.Equal(profile.FishMarkerTemplatePng, loaded.FishMarkerTemplatePng);
            Assert.Equal(profile.ControlMarkerTemplatePng, loaded.ControlMarkerTemplatePng);
            Assert.True(loaded.IsWorldPhaseCalibrated);
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
    public async Task LoadOrDefault_ReturnsEmptyProfileForInvalidTemplateBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            path,
            """{"FishMarkerTemplatePng":"AQID"}""",
            TestContext.Current.CancellationToken);

        try
        {
            var profile = await new VisionProfileStore(path).LoadOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.False(profile.IsFishingCalibrated);
            Assert.Null(profile.FishMarkerTemplatePng);
            Assert.Equal(NormalizedRegion.FullFrame, profile.FishingRegion);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] CreatePngTemplate()
    {
        using var image = new OpenCvSharp.Mat(8, 8, OpenCvSharp.MatType.CV_8UC1, OpenCvSharp.Scalar.All(0));
        OpenCvSharp.Cv2.Rectangle(image, new OpenCvSharp.Rect(1, 1, 6, 6), OpenCvSharp.Scalar.All(255), thickness: -1);
        Assert.True(OpenCvSharp.Cv2.ImEncode(".png", image, out var bytes));
        return bytes;
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
