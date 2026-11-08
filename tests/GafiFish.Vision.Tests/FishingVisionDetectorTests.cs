using GafiFish.Vision.Calibration;
using GafiFish.Vision.Detection;
using GafiFish.Windows.Capture;
using OpenCvSharp;
using Xunit;

namespace GafiFish.Vision.Tests;

public sealed class FishingVisionDetectorTests
{
    [Fact]
    public void Detect_DoesNotGuessMarkerColorsBeforeCalibration()
    {
        var frame = new ScreenCaptureFrame(
            4, 4, 16, new byte[64],
            new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero),
            "Roblox", 1234);

        var result = new FishingVisionDetector().Detect(frame, new VisionProfile());

        Assert.False(result.IsCalibrated);
        Assert.Null(result.FishMarker);
        Assert.Null(result.ControlMarker);
        Assert.Contains("Calibrate", result.Status);
    }

    [Fact]
    public void Detect_RejectsInvalidPixelBuffer()
    {
        var frame = new ScreenCaptureFrame(
            4, 4, 16, new byte[8],
            DateTimeOffset.UtcNow, "Roblox", 1234);

        Assert.Throws<ArgumentException>(
            () => new FishingVisionDetector().Detect(frame, new VisionProfile()));
    }

    [Fact]
    public void Detect_UsesSavedTemplatesWhenColourSamplesAreUnavailable()
    {
        var fishTemplate = CreateTemplate(fish: true);
        var controlTemplate = CreateTemplate(fish: false);
        var frame = CreateFrame(fishTemplate, controlTemplate);

        var profile = new VisionProfile
        {
            FishingRegion = new NormalizedRegion(0, 0, 1, 1),
            FishingRegionCalibrated = true,
            FishMarkerTemplatePng = fishTemplate,
            ControlMarkerTemplatePng = controlTemplate,
            ReferenceWidth = frame.Width,
            ReferenceHeight = frame.Height
        };

        var result = new FishingVisionDetector().Detect(frame, profile);

        Assert.True(result.IsCalibrated);
        Assert.NotNull(result.FishMarker);
        Assert.NotNull(result.ControlMarker);
        Assert.InRange(result.FishMarker.CenterX, 24, 36);
        Assert.InRange(result.FishMarker.CenterY, 19, 31);
        Assert.InRange(result.ControlMarker.CenterX, 74, 86);
        Assert.InRange(result.ControlMarker.CenterY, 44, 56);
        Assert.True(result.FishMarker.Confidence >= 0.82);
        Assert.True(result.ControlMarker.Confidence >= 0.82);
    }

    [Fact]
    public void Detect_RetainsOrientedGeometryForRotatedControlBar()
    {
        const int width = 120;
        const int height = 100;
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
            pixels[i + 3] = 255;

        var rotatedBar = new RotatedRect(
            new Point2f(70, 50),
            new Size2f(64, 8),
            45);
        var corners = rotatedBar.Points()
            .Select(point => new Point((int)Math.Round(point.X), (int)Math.Round(point.Y)))
            .ToArray();
        using (var bgra = new Mat(height, width, MatType.CV_8UC4, Scalar.All(0)))
        {
            Cv2.Rectangle(bgra, new Rect(12, 43, 8, 8), new Scalar(0, 0, 255, 255), thickness: -1);
            Cv2.FillConvexPoly(bgra, corners, new Scalar(255, 0, 0, 255));
            System.Runtime.InteropServices.Marshal.Copy(
                bgra.Data, pixels, 0, pixels.Length);
        }

        var frame = new ScreenCaptureFrame(
            width, height, width * 4, pixels, DateTimeOffset.UtcNow, "Roblox", 1234);
        var profile = new VisionProfile
        {
            FishingRegion = new NormalizedRegion(0, 0, 1, 1),
            FishingRegionCalibrated = true,
            FishMarkerColor = HsvColorSampler.FromBgra(0, 0, 255),
            ControlMarkerColor = HsvColorSampler.FromBgra(255, 0, 0),
            MinimumBlobArea = 5
        };

        var result = new FishingVisionDetector().Detect(frame, profile);

        Assert.NotNull(result.ControlMarker);
        Assert.NotNull(result.ControlMarker.OrientedBounds);
        Assert.InRange(Math.Abs(result.ControlMarker.OrientedBounds.Value.Angle), 35, 55);
    }

    [Fact]
    public void Detect_DualFishModeFindsTwoMarkersWithSeparateColourSamples()
    {
        var result = new FishingVisionDetector().Detect(CreateDualFishFrame(false, true), CreateDualFishProfile(false));
        Assert.True(result.IsCalibrated);
        Assert.NotNull(result.FishMarker);
        Assert.NotNull(result.SecondaryFishMarker);
        Assert.NotNull(result.ControlMarker);
        var positions = new[] { result.FishMarker.CenterX, result.SecondaryFishMarker.CenterX }.Order().ToArray();
        Assert.InRange(positions[0], 18, 32);
        Assert.InRange(positions[1], 78, 92);
        Assert.Contains("Both fish targets", result.Status);
    }

    [Fact]
    public void Detect_DualFishModeFindsTwoMarkersWithTheSameColour()
    {
        var result = new FishingVisionDetector().Detect(CreateDualFishFrame(true, true), CreateDualFishProfile(true));
        Assert.NotNull(result.FishMarker);
        Assert.NotNull(result.SecondaryFishMarker);
        Assert.NotEqual(result.FishMarker.CenterX, result.SecondaryFishMarker.CenterX);
    }

    [Fact]
    public void Detect_DualFishModePausesWhenTheSecondTargetIsMissing()
    {
        var result = new FishingVisionDetector().Detect(CreateDualFishFrame(false, false), CreateDualFishProfile(false));
        Assert.True(result.IsCalibrated);
        Assert.NotNull(result.FishMarker);
        Assert.Null(result.SecondaryFishMarker);
        Assert.Contains("Only one", result.Status);
    }

    [Fact]
    public void Detect_DualFishModeRequiresBothFishColoursAndTheControlColour()
    {
        var result = new FishingVisionDetector().Detect(CreateDualFishFrame(false, true), new VisionProfile
        {
            RodMinigameMode = GafiFish.Core.Control.RodMinigameMode.DualFish
        });
        Assert.False(result.IsCalibrated);
        Assert.Null(result.FishMarker);
        Assert.Null(result.SecondaryFishMarker);
    }

    private static VisionProfile CreateDualFishProfile(bool sameColour) => new()
    {
        RodMinigameMode = GafiFish.Core.Control.RodMinigameMode.DualFish,
        FishingRegion = new NormalizedRegion(0, 0, 1, 1),
        FishingRegionCalibrated = true,
        FishMarkerColor = HsvColorSampler.FromBgra(0, 0, 255),
        SecondaryFishMarkerColor = HsvColorSampler.FromBgra(0, sameColour ? (byte)0 : (byte)255, 255),
        ControlMarkerColor = HsvColorSampler.FromBgra(255, 0, 0),
        MinimumBlobArea = 5
    };

    private static ScreenCaptureFrame CreateDualFishFrame(bool sameColour, bool includeSecond)
    {
        const int width = 110;
        const int height = 70;
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4) pixels[i + 3] = 255;
        FillRect(pixels, width, 14, 24, 10, 8, 0, 0, 255);
        if (includeSecond)
            FillRect(pixels, width, 74, 24, 10, 8, 0, sameColour ? (byte)0 : (byte)255, 255);
        FillRect(pixels, width, 35, 50, 40, 6, 255, 0, 0);
        return new ScreenCaptureFrame(width, height, width * 4, pixels, DateTimeOffset.UtcNow, "Roblox", 1234);
    }

    private static void FillRect(byte[] pixels, int width, int left, int top, int rw, int rh, byte b, byte g, byte r)
    {
        for (var y = top; y < top + rh; y++)
        for (var x = left; x < left + rw; x++)
        {
            var index = (y * width + x) * 4;
            pixels[index] = b;
            pixels[index + 1] = g;
            pixels[index + 2] = r;
            pixels[index + 3] = 255;
        }
    }

    private static byte[] CreateTemplate(bool fish)
    {
        using var template = new Mat(12, 12, MatType.CV_8UC1, Scalar.All(0));
        if (fish)
        {
            Cv2.Rectangle(template, new Rect(2, 4, 8, 4), Scalar.All(255), thickness: -1);
            Cv2.Rectangle(template, new Rect(5, 1, 2, 10), Scalar.All(170), thickness: -1);
        }
        else
        {
            Cv2.Rectangle(template, new Rect(1, 1, 10, 3), Scalar.All(255), thickness: -1);
            Cv2.Rectangle(template, new Rect(4, 1, 4, 10), Scalar.All(130), thickness: -1);
        }

        Assert.True(Cv2.ImEncode(".png", template, out var bytes));
        return bytes;
    }

    private static ScreenCaptureFrame CreateFrame(byte[] fishTemplate, byte[] controlTemplate)
    {
        const int width = 110;
        const int height = 80;
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var index = (y * width + x) * 4;
            pixels[index + 3] = 255;
        }

        BlitTemplate(pixels, width, fishTemplate, left: 24, top: 19);
        BlitTemplate(pixels, width, controlTemplate, left: 74, top: 44);
        return new ScreenCaptureFrame(
            width, height, width * 4, pixels,
            DateTimeOffset.UtcNow, "Roblox", 1234);
    }

    private static void BlitTemplate(byte[] target, int targetWidth, byte[] templatePng, int left, int top)
    {
        using var template = Cv2.ImDecode(templatePng, ImreadModes.Grayscale);
        for (var y = 0; y < template.Height; y++)
        for (var x = 0; x < template.Width; x++)
        {
            var value = template.At<byte>(y, x);
            var index = ((top + y) * targetWidth + left + x) * 4;
            target[index] = value;
            target[index + 1] = value;
            target[index + 2] = value;
        }
    }
}
