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
