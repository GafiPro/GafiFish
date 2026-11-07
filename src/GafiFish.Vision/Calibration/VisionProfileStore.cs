using System.Text.Json;

namespace GafiFish.Vision.Calibration;

/// <summary>
/// Stores calibration locally in the user's AppData directory.
/// No credentials, screenshots, or remote services are involved.
/// </summary>
public sealed class VisionProfileStore
{
    private readonly string _profilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public VisionProfileStore(string? profilePath = null)
    {
        _profilePath = profilePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GafiFish",
                "vision-profile.json");
    }

    public async Task<VisionProfile> LoadOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_profilePath))
            {
                return new VisionProfile();
            }

            await using var stream = new FileStream(
                _profilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);

            var profile = await JsonSerializer.DeserializeAsync<VisionProfile>(
                stream, JsonOptions, cancellationToken);

            profile ??= new VisionProfile();
            profile.Validate();
            return profile;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new VisionProfile();
        }
    }

    public async Task SaveAsync(VisionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        var directory = Path.GetDirectoryName(_profilePath)
            ?? throw new InvalidOperationException("The profile path has no parent directory.");

        Directory.CreateDirectory(directory);

        var temporaryPath = _profilePath + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _profilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
