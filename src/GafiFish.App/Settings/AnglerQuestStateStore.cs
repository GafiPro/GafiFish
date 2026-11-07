using System.IO;
using System.Text.Json;
using GafiFish.Core.Automation;

namespace GafiFish.App.Settings;

/// <summary>
/// Atomically persists Angler request state so a restart cannot silently
/// forget an unfinished hand-in or the 120-second cooldown.
/// </summary>
public sealed class AnglerQuestStateStore
{
    private const int MaximumFileBytes = 8 * 1024;
    private readonly string _statePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AnglerQuestStateStore(string? statePath = null)
    {
        _statePath = statePath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GafiFish", "angler-quest-state.json");
    }

    public async Task<AnglerQuestSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statePath))
            return AnglerQuestSnapshot.Empty;
        var info = new FileInfo(_statePath);
        if (info.Length is <= 0 or > MaximumFileBytes)
            throw new InvalidDataException("The Angler state file is empty or exceeds the 8 KiB safety limit.");

        await using var stream = new FileStream(
            _statePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, useAsync: true);
        var snapshot = await JsonSerializer.DeserializeAsync<AnglerQuestSnapshot>(stream, JsonOptions, cancellationToken);
        if (snapshot is null)
            throw new InvalidDataException("The Angler state file did not contain a valid state object.");
        snapshot.Validate();
        return snapshot;
    }

    public void Save(AnglerQuestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        var directory = Path.GetDirectoryName(_statePath)
            ?? throw new InvalidOperationException("The Angler state path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _statePath + ".tmp";
        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, JsonOptions));
            if (bytes.Length > MaximumFileBytes)
                throw new InvalidDataException("The Angler state snapshot exceeds the 8 KiB safety limit.");
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, options: FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
