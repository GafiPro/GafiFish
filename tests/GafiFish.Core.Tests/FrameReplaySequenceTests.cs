using GafiFish.Core.Diagnostics;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class FrameReplaySequenceTests
{
    [Fact]
    public void NavigatesInSuppliedOrder()
    {
        using var files = new TemporaryImages("frame_001.png", "frame_002.png", "frame_003.png");
        var sequence = FrameReplaySequence.Create(files.Paths);

        Assert.Equal(3, sequence.Count);
        Assert.Equal(0, sequence.CurrentIndex);
        Assert.Equal(files.Paths[0], sequence.CurrentPath);
        Assert.False(sequence.HasPrevious);
        Assert.True(sequence.HasNext);

        Assert.True(sequence.MoveNext());
        Assert.Equal(files.Paths[1], sequence.CurrentPath);
        Assert.True(sequence.MoveNext());
        Assert.Equal(files.Paths[2], sequence.CurrentPath);
        Assert.False(sequence.HasNext);
        Assert.False(sequence.MoveNext());
    }

    [Fact]
    public void CanMoveBackwardsAndRestart()
    {
        using var files = new TemporaryImages("one.jpg", "two.jpg");
        var sequence = FrameReplaySequence.Create(files.Paths);

        Assert.True(sequence.MoveNext());
        Assert.True(sequence.HasPrevious);
        Assert.True(sequence.MovePrevious());
        Assert.Equal(0, sequence.CurrentIndex);
        Assert.False(sequence.MovePrevious());

        Assert.True(sequence.MoveNext());
        sequence.Restart();
        Assert.Equal(0, sequence.CurrentIndex);
        Assert.Equal(files.Paths[0], sequence.CurrentPath);
    }

    [Fact]
    public void DuplicatePathsAreIncludedOnlyOnce()
    {
        using var files = new TemporaryImages("same.bmp");
        var sequence = FrameReplaySequence.Create([files.Paths[0], files.Paths[0]]);

        Assert.Equal(1, sequence.Count);
    }

    [Fact]
    public void RejectsUnsupportedImageExtensions()
    {
        using var files = new TemporaryImages("frame.gif");

        Assert.Throws<ArgumentException>(() => FrameReplaySequence.Create(files.Paths));
    }

    [Fact]
    public void RejectsMissingFiles()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");

        Assert.Throws<FileNotFoundException>(() => FrameReplaySequence.Create([missing]));
    }

    [Fact]
    public void RejectsAnEmptySequence()
    {
        Assert.Throws<ArgumentException>(() => FrameReplaySequence.Create([]));
    }

    private sealed class TemporaryImages(params string[] names) : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "GafiFish-frames-" + Guid.NewGuid().ToString("N"));

        public string[] Paths { get; } = [];

        public TemporaryImages(params string[] names) : this()
        {
            Directory.CreateDirectory(_directory);
            Paths = names.Select(name =>
            {
                var path = Path.Combine(_directory, name);
                File.WriteAllBytes(path, []);
                return path;
            }).ToArray();
        }

        private TemporaryImages() { }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}
