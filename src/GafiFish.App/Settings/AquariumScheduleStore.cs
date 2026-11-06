using System.IO;
using System.Text.Json;
using GafiFish.Core.Aquarium;

namespace GafiFish.App.Settings;

public sealed class AquariumScheduleStore
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AquariumScheduleStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GafiFish",
                "aquarium-schedule.json");
    }

    public async Task<AquariumScheduleState> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AquariumScheduleState();

            await using var stream = new FileStream(
                _settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);
            var state = await JsonSerializer.DeserializeAsync<AquariumScheduleState>(
                stream, JsonOptions, cancellationToken);

            state ??= new AquariumScheduleState();
            state.Validate();
            return state;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new AquariumScheduleState();
        }
    }

    public async Task SaveAsync(
        AquariumScheduleState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();

        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The aquarium settings path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
