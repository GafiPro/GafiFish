using System.Text;

namespace GafiFish.App.Logging;

/// <summary>
/// Stores a small rolling local activity log. The active file and one previous
/// file are kept so the log survives application restarts without growing forever.
/// </summary>
public sealed class ActivityLogStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private readonly string _archivePath;
    private readonly int _maximumFileBytes;

    public ActivityLogStore(string? path = null, int maximumFileBytes = 1024 * 1024)
    {
        if (maximumFileBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumFileBytes));

        _path = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GafiFish", "logs", "activity.log"));
        _archivePath = _path + ".1";
        _maximumFileBytes = maximumFileBytes;
    }

    public string DirectoryPath => Path.GetDirectoryName(_path)
        ?? throw new InvalidOperationException("The activity log path has no parent directory.");

    public IReadOnlyList<string> LoadRecent(int maximumEntries = 500)
    {
        if (maximumEntries is < 1 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));

        lock (_gate)
        {
            var entries = new List<string>();
            if (File.Exists(_archivePath))
                entries.AddRange(File.ReadAllLines(_archivePath, Encoding.UTF8));
            if (File.Exists(_path))
                entries.AddRange(File.ReadAllLines(_path, Encoding.UTF8));

            return entries.TakeLast(maximumEntries).ToArray();
        }
    }

    public void Append(string entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entry);

        var safeEntry = entry.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        var encoded = safeEntry + Environment.NewLine;
        var bytesToAppend = Encoding.UTF8.GetByteCount(encoded);

        lock (_gate)
        {
            Directory.CreateDirectory(DirectoryPath);

            var currentSize = File.Exists(_path) ? new FileInfo(_path).Length : 0L;
            if (currentSize > 0 && currentSize + bytesToAppend > _maximumFileBytes)
            {
                if (File.Exists(_archivePath))
                    File.Delete(_archivePath);
                File.Move(_path, _archivePath);
            }

            File.AppendAllText(_path, encoded, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (File.Exists(_path))
                File.Delete(_path);
            if (File.Exists(_archivePath))
                File.Delete(_archivePath);
        }
    }
}
