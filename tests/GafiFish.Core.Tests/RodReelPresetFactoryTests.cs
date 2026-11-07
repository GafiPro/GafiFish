using GafiFish.Core.Control;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class RodReelPresetFactoryTests
{
    [Theory]
    [InlineData(RodReelPreset.Balanced)]
    [InlineData(RodReelPreset.Precision)]
    [InlineData(RodReelPreset.HeavyFish)]
    [InlineData(RodReelPreset.Stable)]
    public void Create_ReturnsValidSettings(RodReelPreset preset)
    {
        var settings = RodReelPresetFactory.Create(preset);
        settings.Validate();
        Assert.True(settings.MinimumConfidence is >= 0 and <= 1);
    }

    [Fact]
    public void Presets_ProduceDistinctControllerTuning()
    {
        var settings = Enum.GetValues<RodReelPreset>()
            .Select(RodReelPresetFactory.Create)
            .ToArray();

        Assert.Equal(settings.Length, settings
            .Select(value => (value.ProportionalGain, value.DerivativeGain, value.Deadband, value.LookAheadSeconds))
            .Distinct()
            .Count());
    }

    [Fact]
    public void Create_RejectsUnknownPreset()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RodReelPresetFactory.Create((RodReelPreset)999));
    }
}
