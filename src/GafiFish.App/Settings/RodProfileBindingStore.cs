using System.IO;
using System.Text.Json;

namespace GafiFish.App.Settings;

/// <summary>
/// Maps the user's fishing-rod hotbar slots to named local calibration profiles.
/// The mapping is explicit and never attempts to identify a rod from uncalibrated pixels.
/// </summary>
public sealed record RodProfileSlotBindings
{
    public Dictionary<int, string> SlotProfiles { get; init; } = [];
    public Dictionary<string, string> RodNameProfiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(SlotProfiles);
        ArgumentNullException.ThrowIfNull(RodNameProfiles);
        if (RodNameProfiles.Count > 100)
            throw new ArgumentException("At most one hundred rod-name mappings can be stored.");
        if (SlotProfiles.Count > 10)
            throw new ArgumentException("At most ten rod hotbar slots can be mapped.");

        foreach (var (slot, profileName) in SlotProfiles)
        {
            if (slot is < 0 or > 9)
                throw new ArgumentOutOfRangeException(nameof(SlotProfiles), "Rod slots must be between 0 and 9.");
            ValidateProfileName(profileName);
        }

        foreach (var (rodName, profileName) in RodNameProfiles)
        {
            var normalizedName = RodNameMatcher.NormalizeName(rodName);
            if (normalizedName.Length is < 3 or > 64
                || !string.Equals(normalizedName, rodName, StringComparison.Ordinal))
                throw new ArgumentException("Rod-name mapping keys must be normalized OCR names of 3–64 letters or digits.");
            ValidateProfileName(profileName);
        }
    }

    private static void ValidateProfileName(string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || profileName.Trim().Length is < 2 or > 48)
            throw new ArgumentException("Profile names must contain 2–48 non-whitespace characters.");
        var slug = new string(profileName.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        if (slug.Trim('-').Length is < 2 or > 48)
            throw new ArgumentException("The profile name must produce a valid profile file name.");
    }
}

/// <summary>
/// Persists rod-slot/profile associations under the local GafiFish data folder.
/// Writes are atomic so a partial update cannot corrupt the previous mapping.
/// </summary>
public sealed class RodProfileBindingStore
{
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public RodProfileBindingStore(string? settingsPath = null)
    {
        _settingsPath = settingsPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GafiFish", "rod-profile-bindings.json");
    }

    public async Task<RodProfileSlotBindings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new RodProfileSlotBindings();

            await using var stream = new FileStream(_settingsPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 4096, useAsync: true);
            var bindings = await JsonSerializer.DeserializeAsync<RodProfileSlotBindings>(
                stream, JsonOptions, cancellationToken);
            bindings ??= new RodProfileSlotBindings();
            bindings.Validate();
            return bindings;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new RodProfileSlotBindings();
        }
    }

    public async Task SaveAsync(
        RodProfileSlotBindings bindings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        bindings.Validate();

        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The rod profile binding path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _settingsPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write,
                FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, bindings, JsonOptions, cancellationToken);
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
