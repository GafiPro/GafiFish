using GafiFish.Core.Diagnostics;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class VideoReplaySessionTests
{
    [Fact]
    public void NavigatesFramesWithoutLoadingTheWholeVideo()
    {
        using var reader = new FakeVideoReader(frameCount: 5);
        using var session = new VideoReplaySession(reader);

        Assert.Equal(30d, session.FramesPerSecond);
        Assert.Equal(0, session.PlaybackFrameStep);
        Assert.Equal(TimeSpan.FromMilliseconds(1000d / 30d), session.PlaybackInterval);
        Assert.True(session.TryMoveNext(out var first));
        Assert.Equal(0, session.CurrentIndex);
        Assert.Equal(0, first!.FrameIndex);
        Assert.False(session.HasPrevious);
        Assert.True(session.HasNext);

        Assert.True(session.TryMoveNext(out var second));
        Assert.Equal(1, session.CurrentIndex);
        Assert.Equal(1, second!.FrameIndex);
        Assert.True(session.TryMovePrevious(out var previous));
        Assert.Equal(0, previous!.FrameIndex);
        Assert.False(session.TryMovePrevious(out _));
    }

    [Fact]
    public void SupportsSeekingAndReportsEndOfVideo()
    {
        using var reader = new FakeVideoReader(frameCount: 10, framesPerSecond: 60);
        using var session = new VideoReplaySession(reader);

        Assert.Equal(2, session.PlaybackFrameStep);
        Assert.True(session.TryMoveTo(9, out var last));
        Assert.Equal(9, last!.FrameIndex);
        Assert.False(session.HasNext);
        Assert.True(session.HasPrevious);
        Assert.False(session.TryMoveTo(10, out _));
        Assert.Equal(9, session.CurrentIndex);
    }

    [Fact]
    public void InvalidFrameMetadataDisposesReader()
    {
        var reader = new FakeVideoReader(frameCount: 0);

        Assert.Throws<ArgumentException>(() => new VideoReplaySession(reader));
        Assert.True(reader.IsDisposed);
    }

    [Fact]
    public void InvalidFpsUsesSafeDefault()
    {
        using var reader = new FakeVideoReader(frameCount: 2, framesPerSecond: double.NaN);
        using var session = new VideoReplaySession(reader);

        Assert.Equal(30d, session.FramesPerSecond);
        Assert.Equal(1, session.PlaybackFrameStep);
    }

    [Fact]
    public void InvalidDecodedFrameIsRejectedWithoutChangingCurrentIndex()
    {
        using var reader = new FakeVideoReader(frameCount: 3);
        using var session = new VideoReplaySession(reader);
        Assert.True(session.TryMoveTo(0, out _));
        reader.ReturnInvalidFrame = true;

        Assert.Throws<InvalidDataException>(() => session.TryMoveTo(1, out _));
        Assert.Equal(0, session.CurrentIndex);
    }

    [Fact]
    public void DisposeClosesReaderAndFurtherReadsAreRejected()
    {
        var reader = new FakeVideoReader(frameCount: 2);
        var session = new VideoReplaySession(reader);

        session.Dispose();

        Assert.True(reader.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => session.TryMoveTo(0, out _));
    }

    private sealed class FakeVideoReader(
        int frameCount,
        double framesPerSecond = 30d) : IVideoFrameReader
    {
        public int Width => 64;
        public int Height => 64;
        public int FrameCount => frameCount;
        public double FramesPerSecond => framesPerSecond;
        public bool IsDisposed { get; private set; }
        public bool ReturnInvalidFrame { get; set; }

        public bool TryReadFrame(int frameIndex, out VideoReplayFrame? frame)
        {
            if (IsDisposed || frameIndex < 0 || frameIndex >= FrameCount)
            {
                frame = null;
                return false;
            }

            var stride = Width * 4;
            var pixels = new byte[stride * Height];
            frame = new VideoReplayFrame(
                ReturnInvalidFrame ? Width + 1 : Width,
                Height,
                stride,
                pixels,
                frameIndex);
            return true;
        }

        public void Dispose() => IsDisposed = true;
    }
}
