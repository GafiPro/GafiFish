using System.Text.Json;
using System.Text.Json.Serialization;
using GafiFish.Core.Models;
using GafiFish.Data.Export;
using Xunit;

namespace GafiFish.Data.Tests;

public sealed class SessionExportServiceTests
{
    [Fact]
    public async Task ExportCsvAsync_EscapesCommasQuotesAndKeepsUnknownFieldsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.csv");
        var catches = new[]
        {
            new FishCatch(
                Guid.NewGuid(),
                new DateTimeOffset(2026, 11, 5, 20, 15, 0, TimeSpan.Zero),
                "Golden, \"Koi\"",
                FishRarity.Secret,
                null,
                null,
                1234.5m,
                0.98)
        };

        try
        {
            await SessionExportService.ExportCsvAsync(
                catches, path, TestContext.Current.CancellationToken);

            var lines = await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(2, lines.Length);
            Assert.Contains("\"Golden, \"\"Koi\"\"", lines[1]);
            Assert.Contains(",Secret,,", lines[1]);
            Assert.Contains(",0.98", lines[1]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ExportJsonAsync_PreservesNullableFieldsAndEnumNames()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-{Guid.NewGuid():N}.json");
        var catches = new[]
        {
            new FishCatch(
                Guid.NewGuid(),
                new DateTimeOffset(2026, 11, 5, 20, 15, 0, TimeSpan.Zero),
                "Bluefin Tuna",
                FishRarity.Legendary,
                "Shiny",
                12.5,
                null,
                0.87)
        };

        try
        {
            await SessionExportService.ExportJsonAsync(
                catches, path, TestContext.Current.CancellationToken);

            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(
                stream, cancellationToken: TestContext.Current.CancellationToken);

            var fish = document.RootElement[0];
            Assert.Equal("Bluefin Tuna", fish.GetProperty("Species").GetString());
            Assert.Equal("Legendary", fish.GetProperty("Rarity").GetString());
            Assert.Equal("Shiny", fish.GetProperty("Mutation").GetString());
            Assert.True(fish.GetProperty("EstimatedValue").ValueKind == JsonValueKind.Null);
            Assert.Equal(0.87, fish.GetProperty("RecognitionConfidence").GetDouble(), precision: 2);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
