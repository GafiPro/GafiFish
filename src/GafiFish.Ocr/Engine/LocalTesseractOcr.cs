using System.Runtime.InteropServices;
using GafiFish.Ocr.Models;
using GafiFish.Vision.Calibration;
using GafiFish.Windows.Capture;
using OpenCvSharp;
using TesseractOCR;
using TesseractOCR.Enums;
using PixImage = TesseractOCR.Pix.Image;

namespace GafiFish.Ocr.Engine;

/// <summary>
/// Local English OCR over a selected part of a captured Roblox frame.
/// Screenshots are converted in memory and never uploaded.
/// </summary>
public sealed class LocalTesseractOcr : IDisposable
{
    private readonly object _sync = new();
    private readonly TesseractOCR.Engine _engine;
    private bool _disposed;

    public LocalTesseractOcr(string tessdataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tessdataDirectory);

        var modelPath = Path.Combine(tessdataDirectory, "eng.traineddata");
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                "The English OCR model is missing. Install it before starting OCR.",
                modelPath);
        }

        _engine = new TesseractOCR.Engine(
            tessdataDirectory,
            Language.English,
            EngineMode.Default);

        _engine.DefaultPageSegMode = PageSegMode.SingleBlock;
    }

    public LocalOcrResult Recognize(
        ScreenCaptureFrame frame,
        NormalizedRegion region)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ThrowIfDisposed();
        region.Validate();

        if (frame.Width <= 0 || frame.Height <= 0
            || frame.Stride != frame.Width * 4
            || frame.BgraPixels.Length != checked(frame.Stride * frame.Height))
        {
            throw new ArgumentException("Frame pixel buffer is invalid.", nameof(frame));
        }

        using var bgra = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        Marshal.Copy(frame.BgraPixels, 0, bgra.Data, frame.BgraPixels.Length);

        var pixelRegion = region.ToPixelRect(frame.Width, frame.Height);
        using var cropped = new Mat(bgra, pixelRegion);
        using var gray = new Mat();
        Cv2.CvtColor(cropped, gray, ColorConversionCodes.BGRA2GRAY);

        using var enlarged = new Mat();
        Cv2.Resize(
            gray,
            enlarged,
            new Size(Math.Max(1, gray.Width * 2), Math.Max(1, gray.Height * 2)),
            interpolation: InterpolationFlags.Cubic);

        using var thresholded = new Mat();
        Cv2.Threshold(
            enlarged,
            thresholded,
            0,
            255,
            ThresholdTypes.Binary | ThresholdTypes.Otsu);

        if (!Cv2.ImEncode(".png", thresholded, out var encodedImage))
        {
            throw new InvalidOperationException("Could not encode the OCR image.");
        }

        using var image = PixImage.LoadFromMemory(encodedImage);

        lock (_sync)
        {
            ThrowIfDisposed();

            using var page = _engine.Process(image, PageSegMode.SingleBlock);
            return new LocalOcrResult(
                page.Text ?? string.Empty,
                Math.Clamp(page.MeanConfidence, 0, 1),
                region,
                frame.CapturedAt);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _engine.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
