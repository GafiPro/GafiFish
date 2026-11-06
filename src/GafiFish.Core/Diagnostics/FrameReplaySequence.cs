using System.IO;

namespace GafiFish.Core.Diagnostics;

/// <summary>
/// Ordered list of image files used for offline, preview-only frame replay.
/// </summary>
public sealed class FrameReplaySequence
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp" };

    private readonly string[] _paths;
    private int _currentIndex;

    private FrameReplaySequence(string[] paths)
    {
        _paths = paths;
        _currentIndex = 0;
    }

    public int Count => _paths.Length;
    public int CurrentIndex => _currentIndex;
    public string CurrentPath => _paths[_currentIndex];
    public bool HasPrevious => _currentIndex > 0;
    public bool HasNext => _currentIndex < _paths.Length - 1;

    public static FrameReplaySequence Create(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Frame paths cannot be empty.", nameof(paths));

            var fullPath = Path.GetFullPath(path);
            if (!SupportedExtensions.Contains(Path.GetExtension(fullPath)))
                throw new ArgumentException($"Unsupported frame image format: {Path.GetExtension(fullPath)}.", nameof(paths));

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("A selected frame image does not exist.", fullPath);

            if (seen.Add(fullPath))
                normalized.Add(fullPath);

            if (normalized.Count > 2500)
                throw new ArgumentException("A replay sequence cannot contain more than 2500 images.", nameof(paths));
        }

        if (normalized.Count == 0)
            throw new ArgumentException("Select at least one frame image.", nameof(paths));

        return new FrameReplaySequence(normalized.ToArray());
    }

    public bool MoveNext()
    {
        if (!HasNext)
            return false;

        _currentIndex++;
        return true;
    }

    public bool MovePrevious()
    {
        if (!HasPrevious)
            return false;

        _currentIndex--;
        return true;
    }

    public void Restart() => _currentIndex = 0;
}
