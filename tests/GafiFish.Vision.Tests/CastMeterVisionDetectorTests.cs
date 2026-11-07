using GafiFish.Core.Casting;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class CastMeterVisionDetectorTests
{
    [Fact]
    public void Detect_ReturnsNormalisedIndicatorAndTargetPositions()
    {
        var frame = CreateFrame(
            new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero),
            indicatorLeft: 20);
        var profile = CreateProfile();

        var observation = new CastMeterVisionDetector().Detect(frame, profile);

        Assert.NotNull(observation);
        Assert.True(observation.IsVisible);
        Assert.InRange(observation.IndicatorPosition, 0.20, 0.25);
        Assert.InRange(observation.TargetStart, 0.69, 0.72);
        Assert.InRange(observation.TargetEnd, 0.84, 0.87);
        Assert.True(observation.Confidence > 0.5);
    }

    [Fact]
    public void Detect_EstimatesSignedIndicatorVelocityBetweenFrames()
    {
        var detector = new CastMeterVisionDetector();
        var profile = CreateProfile();
        var start = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

        _ = detector.Detect(CreateFrame(start, indicatorLeft: 20), profile);
        var next = detector.Detect(CreateFrame(start.AddMilliseconds(100), indicatorLeft: 30), profile);

        Assert.NotNull(next);
        Assert.InRange(next.IndicatorVelocityPerSecond, 0.9, 1.1);
    }

    [Fact]
    public void Detect_IgnoresOutOfOrderFramesWithoutCorruptingVelocity()
    {
        var detector = new CastMeterVisionDetector();
        var profile = CreateProfile();
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        _ = detector.Detect(CreateFrame(start, indicatorLeft: 20), profile);
        var latest = detector.Detect(CreateFrame(start.AddMilliseconds(100), indicatorLeft: 30), profile);
        Assert.NotNull(latest);
        Assert.InRange(latest.IndicatorVelocityPerSecond, 0.9, 1.1);

        Assert.Null(detector.Detect(CreateFrame(start.AddMilliseconds(50), indicatorLeft: 90), profile));

        var next = detector.Detect(CreateFrame(start.AddMilliseconds(200), indicatorLeft: 40), profile);
        Assert.NotNull(next);
        Assert.InRange(next.IndicatorVelocityPerSecond, 0.9, 1.1);
    }

    [Fact]
    public void Detect_IgnoresDuplicateFramesWithoutUpdatingVelocityBaseline()
    {
        var detector = new CastMeterVisionDetector();
        var profile = CreateProfile();
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        _ = detector.Detect(CreateFrame(start, indicatorLeft: 20), profile);
        var duplicate = CreateFrame(start, indicatorLeft: 90);
        Assert.Null(detector.Detect(duplicate, profile));

        var next = detector.Detect(CreateFrame(start.AddMilliseconds(100), indicatorLeft: 30), profile);
        Assert.NotNull(next);
        Assert.InRange(next.IndicatorVelocityPerSecond, 0.9, 1.1);
    }

    [Fact]
    public void Reset_ClearsVelocityHistoryBetweenCalibrationProfiles()
    {
        var detector = new CastMeterVisionDetector();
        var profile = CreateProfile();
        var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);

        _ = detector.Detect(CreateFrame(start, indicatorLeft: 20), profile);
        var beforeReset = detector.Detect(CreateFrame(start.AddMilliseconds(100), indicatorLeft: 30), profile);
        Assert.NotNull(beforeReset);
        Assert.InRange(beforeReset.IndicatorVelocityPerSecond, 0.9, 1.1);

        detector.Reset();
        var afterReset = detector.Detect(CreateFrame(start.AddMilliseconds(200), indicatorLeft: 40), profile);

        Assert.NotNull(afterReset);
        Assert.Equal(0, afterReset.IndicatorVelocityPerSecond, precision: 5);
    }

    [Fact]
    public void Detect_ReturnsNullWhenColorsWereSampledButRegionWasNotSelected()
    {
        var frame = CreateFrame(DateTimeOffset.UtcNow, indicatorLeft: 20);
        var profile = new VisionProfile
        {
            CastIndicatorColor = HsvColorSampler.FromBgra(255, 255, 255),
            CastTargetColor = HsvColorSampler.FromBgra(0, 255, 0)
        };

        var observation = new CastMeterVisionDetector().Detect(frame, profile);

        Assert.Null(observation);
    }

    [Fact]
    public void Detect_ReturnsNullUntilBothCastColorsAreCalibrated()
    {
        var frame = CreateFrame(DateTimeOffset.UtcNow, indicatorLeft: 20);

        var observation = new CastMeterVisionDetector().Detect(frame, new VisionProfile());

        Assert.Null(observation);
    }

    private static VisionProfile CreateProfile() => new()
    {
        CastRegion = NormalizedRegion.FullFrame,
        CastRegionCalibrated = true,
        CastIndicatorColor = HsvColorSampler.FromBgra(255, 255, 255),
        CastTargetColor = HsvColorSampler.FromBgra(0, 255, 0),
        MinimumBlobArea = 4,
        MinimumConfidence = 0
    };

    private static ScreenCaptureFrame CreateFrame(DateTimeOffset capturedAt, int indicatorLeft)
    {
        const int width = 100;
        const int height = 50;
        var pixels = new byte[width * height * 4];

        FillRectangle(pixels, width, x: indicatorLeft, y: 15, rectangleWidth: 5, rectangleHeight: 15,
            blue: 255, green: 255, red: 255);
        FillRectangle(pixels, width, x: 70, y: 15, rectangleWidth: 16, rectangleHeight: 15,
            blue: 0, green: 255, red: 0);

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels, capturedAt, "Roblox", 1234);
    }

    private static void FillRectangle(
        byte[] pixels,
        int frameWidth,
        int x,
        int y,
        int rectangleWidth,
        int rectangleHeight,
        byte blue,
        byte green,
        byte red)
    {
        for (var row = y; row < y + rectangleHeight; row++)
        {
            for (var column = x; column < x + rectangleWidth; column++)
            {
                var index = (row * frameWidth + column) * 4;
                pixels[index] = blue;
                pixels[index + 1] = green;
                pixels[index + 2] = red;
                pixels[index + 3] = 255;
            }
        }
    }
}
