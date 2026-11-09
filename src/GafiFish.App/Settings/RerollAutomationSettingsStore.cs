using System.IO;
using System.Text.Json;

namespace GafiFish.App.Settings;

public sealed class RerollAutomationSettingsStore
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public RerollAutomationSettingsStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GafiFish", "reroll-automation-settings.json");
    }

    public async Task<RerollAutomationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new RerollAutomationSettings();

            await using var stream = new FileStream(
                _settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);
            var settings = await JsonSerializer.DeserializeAsync<RerollAutomationSettings>(
                stream, JsonOptions, cancellationToken);
            settings ??= new RerollAutomationSettings();
            settings.Validate();
            return settings;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new RerollAutomationSettings();
        }
    }

    public async Task SaveAsync(
        RerollAutomationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The settings path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
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
