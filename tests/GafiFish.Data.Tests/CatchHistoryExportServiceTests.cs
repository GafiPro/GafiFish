using System.Text.Json;
using GafiFish.Core.Models;
using GafiFish.Data.Export;
using GafiFish.Data.Models;
using Xunit;

namespace GafiFish.Data.Tests;

public sealed class CatchHistoryExportServiceTests
{
    [Fact]
    public async Task ExportCsvAsync_IncludesSessionMetadataAndEscapesSpecies()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.csv");
        var sessionId = Guid.NewGuid();
        var history = new[]
        {
            new FishCatchHistoryRecord(sessionId,
                new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 5, 11, 0, 0, TimeSpan.Zero),
                new FishCatch(Guid.NewGuid(), new DateTimeOffset(2026, 11, 5, 10, 10, 0, TimeSpan.Zero),
                    "Golden, \"Koi\"", FishRarity.Secret, null, null, null, 0.99))
        };

        try
        {
            await CatchHistoryExportService.ExportCsvAsync(history, path, TestContext.Current.CancellationToken);
            var lines = await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(2, lines.Length);
            Assert.Contains("SessionId,SessionStartedAt,SessionEndedAt", lines[0]);
            Assert.Contains(sessionId.ToString("D"), lines[1]);
            Assert.Contains("\"Golden, \"\"Koi\"\"", lines[1]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportJsonAsync_PreservesSessionAndCatchDetails()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.json");
        var sessionId = Guid.NewGuid();
        var history = new[]
        {
            new FishCatchHistoryRecord(sessionId,
                new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero),
                null,
                new FishCatch(Guid.NewGuid(), new DateTimeOffset(2026, 11, 5, 10, 10, 0, TimeSpan.Zero),
                    "Bluefin Tuna", FishRarity.Legendary, "Shiny", 12.5, 500m, 0.98))
        };

        try
        {
            await CatchHistoryExportService.ExportJsonAsync(history, path, TestContext.Current.CancellationToken);
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
            var record = document.RootElement[0];
            Assert.Equal(sessionId.ToString("D"), record.GetProperty("SessionId").GetString());
            Assert.True(record.GetProperty("SessionEndedAt").ValueKind == JsonValueKind.Null);
            Assert.Equal("Bluefin Tuna", record.GetProperty("Catch").GetProperty("Species").GetString());
            Assert.Equal("Legendary", record.GetProperty("Catch").GetProperty("Rarity").GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
