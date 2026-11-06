using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GafiFish.Data.Models;

namespace GafiFish.Data.Export;

/// <summary>
/// Exports the complete local catch archive with its parent session metadata.
/// No network service is used.
/// </summary>
public static class CatchHistoryExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task ExportCsvAsync(
        IReadOnlyList<FishCatchHistoryRecord> history,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        EnsureParentDirectory(destinationPath);

        await using var stream = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 16 * 1024, useAsync: true);
        await using var writer = new StreamWriter(
            stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(
            "SessionId,SessionStartedAt,SessionEndedAt,CatchId,CaughtAt,Species,Rarity,Mutation,WeightKg,EstimatedValue,RecognitionConfidence");

        foreach (var record in history)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fish = record.Catch;
            var fields = new[]
            {
                record.SessionId.ToString("D"),
                record.SessionStartedAt.ToString("O", CultureInfo.InvariantCulture),
                record.SessionEndedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                fish.Id.ToString("D"),
                fish.CaughtAt.ToString("O", CultureInfo.InvariantCulture),
                fish.Species ?? string.Empty,
                fish.Rarity?.ToString() ?? string.Empty,
                fish.Mutation ?? string.Empty,
                fish.WeightKg?.ToString("0.########", CultureInfo.InvariantCulture) ?? string.Empty,
                fish.EstimatedValue?.ToString("0.################", CultureInfo.InvariantCulture) ?? string.Empty,
                fish.RecognitionConfidence.ToString("0.####", CultureInfo.InvariantCulture)
            };
            await writer.WriteLineAsync(string.Join(",", fields.Select(EscapeCsv)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    public static async Task ExportJsonAsync(
        IReadOnlyList<FishCatchHistoryRecord> history,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        EnsureParentDirectory(destinationPath);

        await using var stream = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 16 * 1024, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, history, JsonOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static void EnsureParentDirectory(string destinationPath)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
