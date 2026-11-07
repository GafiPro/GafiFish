using GafiFish.Core.Control;
using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class RhythmNoteVisionDetectorTests
{
    [Fact]
    public void Detect_FindsOneFallingNoteInEachCalibratedLane()
    {
        var profile = CreateProfile();
        var frame = CreateFrame(
            notes: [new(80, 150), new(160, 160), new(240, 170), new(320, 180)],
            includeReceptors: true);

        var result = new RhythmNoteVisionDetector().Detect(frame, profile);

        Assert.True(result.IsCalibrated);
        Assert.Equal(4, result.LaneMarkers.Count);
        Assert.Equal(4, result.DetectedLaneCount);
        Assert.InRange(result.HitLineY, 233.9, 234.1);
        Assert.InRange(result.LaneMarkers[0]!.CenterX, 79, 81);
        Assert.InRange(result.LaneMarkers[1]!.CenterX, 159, 161);
        Assert.InRange(result.LaneMarkers[2]!.CenterX, 239, 241);
        Assert.InRange(result.LaneMarkers[3]!.CenterX, 319, 321);
    }

    [Fact]
    public void Detect_IgnoresStationaryReceptorsOnTheHitLine()
    {
        var profile = CreateProfile();
        var frame = CreateFrame(notes: [], includeReceptors: true);

        var result = new RhythmNoteVisionDetector().Detect(frame, profile);

        Assert.True(result.IsCalibrated);
        Assert.False(result.HasNotes);
        Assert.Equal(0, result.DetectedLaneCount);
    }

    [Fact]
    public void Detect_ReturnsUncalibratedObservationUntilAllFourLaneColoursAndHitLineExist()
    {
        var profile = CreateProfile() with
        {
            RhythmLaneColors = [CreateLaneColour(0), CreateLaneColour(1), null, CreateLaneColour(3)],
            RhythmHitLineCalibrated = false
        };

        var result = new RhythmNoteVisionDetector().Detect(
            CreateFrame(notes: [new(80, 150)], includeReceptors: false),
            profile);

        Assert.False(result.IsCalibrated);
        Assert.False(result.HasNotes);
    }

    [Fact]
    public void VisionProfile_RejectsWrongNumberOfRhythmLaneColours()
    {
        var profile = CreateProfile() with
        {
            RhythmLaneColors = [CreateLaneColour(0), CreateLaneColour(1), CreateLaneColour(2)]
        };

        Assert.Throws<ArgumentException>(() => profile.Validate());
    }

    private static VisionProfile CreateProfile() => new()
    {
        RodMinigameMode = RodMinigameMode.TranquilityRhythm,
        FishingRegion = new NormalizedRegion(0.1, 0.1, 0.8, 0.8),
        FishingRegionCalibrated = true,
        RhythmLaneColors =
        [
            CreateLaneColour(0), CreateLaneColour(1),
            CreateLaneColour(2), CreateLaneColour(3)
        ],
        RhythmHitLinePosition = 0.85,
        RhythmHitLineCalibrated = true,
        MinimumBlobArea = 5,
        MinimumConfidence = 0.60
    };

    private static HsvColorRange CreateLaneColour(int lane)
    {
        var (blue, green, red) = lane switch
        {
            0 => ((byte)0, (byte)0, (byte)255),       // D: red
            1 => ((byte)0, (byte)255, (byte)0),       // F: green
            2 => ((byte)255, (byte)0, (byte)0),       // J: blue
            3 => ((byte)0, (byte)255, (byte)255),     // K: yellow
            _ => throw new ArgumentOutOfRangeException(nameof(lane))
        };

        return HsvColorSampler.FromBgra(
            blue, green, red,
            hueTolerance: 3,
            saturationTolerance: 30,
            valueTolerance: 30);
    }

    private static ScreenCaptureFrame CreateFrame(
        IReadOnlyList<(int X, int Y)> notes,
        bool includeReceptors)
    {
        const int width = 400;
        const int height = 300;
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
            pixels[index + 3] = 255;

        for (var lane = 0; lane < notes.Count; lane++)
            DrawDisc(pixels, width, height, notes[lane].X, notes[lane].Y, 7, GetBgr(lane));

        if (includeReceptors)
        {
            for (var lane = 0; lane < 4; lane++)
                DrawDisc(pixels, width, height, 80 + (lane * 80), 234, 7, GetBgr(lane));
        }

        return new ScreenCaptureFrame(
            width, height, width * 4, pixels,
            DateTimeOffset.UtcNow, "Roblox", 1234);
    }

    private static (byte Blue, byte Green, byte Red) GetBgr(int lane) => lane switch
    {
        0 => (0, 0, 255),
        1 => (0, 255, 0),
        2 => (255, 0, 0),
        3 => (0, 255, 255),
        _ => throw new ArgumentOutOfRangeException(nameof(lane))
    };

    private static void DrawDisc(
        byte[] pixels,
        int width,
        int height,
        int centerX,
        int centerY,
        int radius,
        (byte Blue, byte Green, byte Red) colour)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
                continue;
            var dx = x - centerX;
            var dy = y - centerY;
            if ((dx * dx) + (dy * dy) > radius * radius)
                continue;

            var index = (y * width + x) * 4;
            pixels[index] = colour.Blue;
            pixels[index + 1] = colour.Green;
            pixels[index + 2] = colour.Red;
        }
    }
}
