namespace GafiFish.Core.Diagnostics;

/// <summary>
/// One decoded BGRA frame from an offline video. Pixel memory is owned by the frame.
/// </summary>
public sealed record VideoReplayFrame(
    int Width,
    int Height,
    int Stride,
    byte[] BgraPixels,
    int FrameIndex);

/// <summary>
/// Minimal decoder contract so video replay state can be tested without a codec or game client.
/// Implementations must decode frames by zero-based frame index.
/// </summary>
public interface IVideoFrameReader : IDisposable
{
    int Width { get; }
    int Height { get; }
    int FrameCount { get; }
    double FramesPerSecond { get; }
    bool TryReadFrame(int frameIndex, out VideoReplayFrame? frame);
}

/// <summary>
/// Bounded, one-frame-at-a-time video replay state. It has no game-input or OCR responsibilities.
/// </summary>
public sealed class VideoReplaySession : IDisposable
{
    public const int MaximumFrameCount = 500_000;
    public const int MaximumDimension = 8192;
    private const long MaximumFrameBufferBytes = 256L * 1024 * 1024;

    private readonly IVideoFrameReader _reader;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public int FrameCount { get; }
    public double FramesPerSecond { get; }
    public int CurrentIndex { get; private set; } = -1;
    public VideoReplayFrame? CurrentFrame { get; private set; }
    public bool HasPrevious => CurrentIndex > 0;
    public bool HasNext => CurrentIndex < FrameCount - 1;

    /// <summary>
    /// Limits UI replay to 30 refreshes per second. Faster videos skip source frames
    /// rather than slowing playback or queuing an unbounded UI workload.
    /// </summary>
    public int PlaybackFrameStep => Math.Max(1, (int)Math.Ceiling(FramesPerSecond / 30d));

    public TimeSpan PlaybackInterval =>
        TimeSpan.FromMilliseconds(Math.Clamp(1000d / Math.Min(FramesPerSecond, 30d), 20d, 1000d));

    public VideoReplaySession(IVideoFrameReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;

        try
        {
            if (reader.Width is < 64 or > MaximumDimension
                || reader.Height is < 64 or > MaximumDimension)
            {
                throw new ArgumentException("Video dimensions must be between 64 and 8192 pixels.", nameof(reader));
            }

            if (reader.FrameCount is < 1 or > MaximumFrameCount)
            {
                throw new ArgumentException(
                    $"Video frame count must be between 1 and {MaximumFrameCount}.", nameof(reader));
            }

            Width = reader.Width;
            Height = reader.Height;
            FrameCount = reader.FrameCount;
            FramesPerSecond = double.IsFinite(reader.FramesPerSecond)
                && reader.FramesPerSecond is > 0 and <= 240
                    ? reader.FramesPerSecond
                    : 30d;
        }
        catch
        {
            _reader.Dispose();
            _disposed = true;
            throw;
        }
    }

    public bool TryMoveTo(int frameIndex, out VideoReplayFrame? frame)
    {
        ThrowIfDisposed();
        frame = null;

        if (frameIndex < 0 || frameIndex >= FrameCount)
            return false;

        if (!_reader.TryReadFrame(frameIndex, out var decoded) || decoded is null)
            return false;

        var minimumStride = checked(Width * 4);
        var requiredBytes = (long)decoded.Stride * decoded.Height;
        if (decoded.FrameIndex != frameIndex
            || decoded.Width != Width
            || decoded.Height != Height
            || decoded.Stride < minimumStride
            || decoded.Stride <= 0
            || requiredBytes > MaximumFrameBufferBytes
            || requiredBytes > decoded.BgraPixels.LongLength)
        {
            throw new InvalidDataException("The video decoder returned an invalid BGRA frame.");
        }

        CurrentIndex = frameIndex;
        CurrentFrame = decoded;
        frame = decoded;
        return true;
    }

    public bool TryMoveNext(out VideoReplayFrame? frame) =>
        TryMoveTo(CurrentIndex < 0 ? 0 : CurrentIndex + 1, out frame);

    public bool TryMovePrevious(out VideoReplayFrame? frame) =>
        TryMoveTo(CurrentIndex - 1, out frame);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CurrentFrame = null;
        _reader.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
