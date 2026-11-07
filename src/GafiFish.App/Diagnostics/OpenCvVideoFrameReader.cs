using System.IO;
using System.Runtime.InteropServices;
using GafiFish.Core.Diagnostics;
using OpenCvSharp;

namespace GafiFish.App.Diagnostics;

/// <summary>
/// OpenCV-backed file reader. It keeps only the decoder and one frame in memory.
/// </summary>
internal sealed class OpenCvVideoFrameReader : IVideoFrameReader
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".m4v", ".avi", ".mov", ".mkv", ".wmv", ".webm"
        };

    private readonly VideoCapture _capture;
    private int _lastDecodedIndex = -1;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public int FrameCount { get; }
    public double FramesPerSecond { get; }

    public OpenCvVideoFrameReader(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Select a video file.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        if (!SupportedExtensions.Contains(Path.GetExtension(fullPath)))
            throw new ArgumentException("This video file extension is not supported.", nameof(path));
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The selected video file does not exist.", fullPath);

        _capture = new VideoCapture(fullPath);
        try
        {
            if (!_capture.IsOpened())
                throw new InvalidDataException(
                    "OpenCV could not open this video. Check the file and whether its codec is supported.");

            Width = ReadPositiveInteger(VideoCaptureProperties.FrameWidth);
            Height = ReadPositiveInteger(VideoCaptureProperties.FrameHeight);
            FrameCount = ReadPositiveInteger(VideoCaptureProperties.FrameCount);
            FramesPerSecond = _capture.Get(VideoCaptureProperties.Fps);
        }
        catch
        {
            _capture.Dispose();
            _disposed = true;
            throw;
        }
    }

    public bool TryReadFrame(int frameIndex, out VideoReplayFrame? frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        frame = null;

        if (frameIndex < 0 || frameIndex >= FrameCount)
            return false;

        using var decoded = new Mat();
        if (_lastDecodedIndex != frameIndex - 1
            && !_capture.Set(VideoCaptureProperties.PosFrames, frameIndex))
        {
            return false;
        }

        if (!_capture.Read(decoded) || decoded.Empty())
            return false;

        using var bgra = new Mat();
        switch (decoded.Channels())
        {
            case 1:
                Cv2.CvtColor(decoded, bgra, ColorConversionCodes.GRAY2BGRA);
                break;
            case 3:
                Cv2.CvtColor(decoded, bgra, ColorConversionCodes.BGR2BGRA);
                break;
            case 4:
                decoded.CopyTo(bgra);
                break;
            default:
                return false;
        }

        if (bgra.Type() != MatType.CV_8UC4)
            return false;

        var rows = bgra.Rows;
        var columns = bgra.Cols;
        var stride = checked(columns * 4);
        var sourceStride = checked((int)bgra.Step());
        var pixels = new byte[checked(stride * rows)];

        for (var row = 0; row < rows; row++)
        {
            Marshal.Copy(
                IntPtr.Add(bgra.Data, checked(row * sourceStride)),
                pixels,
                checked(row * stride),
                stride);
        }

        _lastDecodedIndex = frameIndex;
        frame = new VideoReplayFrame(columns, rows, stride, pixels, frameIndex);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _capture.Release();
        _capture.Dispose();
    }

    private int ReadPositiveInteger(VideoCaptureProperties property)
    {
        var value = _capture.Get(property);
        if (!double.IsFinite(value) || value < 1 || value > int.MaxValue)
            throw new InvalidDataException("The video file does not provide valid frame dimensions or frame count.");

        return checked((int)Math.Round(value));
    }
}
