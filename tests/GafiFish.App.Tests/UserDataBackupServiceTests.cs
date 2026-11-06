using System.IO.Compression;
using Microsoft.Data.Sqlite;
using GafiFish.App.Backup;
using Xunit;

namespace GafiFish.App.Tests;

public sealed class UserDataBackupServiceTests
{
    [Fact]
    public async Task CreateBackup_IncludesSqliteSnapshotAndLocalSettings()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            await CreateDatabaseAsync(Path.Combine(directory, "sessions.db"));
            await File.WriteAllTextAsync(Path.Combine(directory, "vision-profile.json"), "{ \"profile\": 1 }");
            await File.WriteAllTextAsync(Path.Combine(directory, "app-settings.json"), "{ \"hotkey\": \"F8\" }");
            await File.WriteAllTextAsync(Path.Combine(directory, "app-settings.json.tmp"), "temporary");
            await File.WriteAllTextAsync(Path.Combine(directory, "old-backup.zip"), "do not nest old backups");
            Directory.CreateDirectory(Path.Combine(directory, "logs"));
            await File.WriteAllTextAsync(Path.Combine(directory, "logs", "activity.log"), "INFO saved");

            var backupPath = Path.Combine(Path.GetTempPath(), $"gafifish-backup-{Guid.NewGuid():N}.zip");
            try
            {
                var service = new UserDataBackupService(directory);
                var manifest = await service.CreateBackupAsync(backupPath);

                Assert.Contains(manifest.Files, item => item.Path == "sessions.db");
                Assert.Contains(manifest.Files, item => item.Path == "vision-profile.json");
                Assert.Contains(manifest.Files, item => item.Path == "logs/activity.log");
                Assert.DoesNotContain(manifest.Files, item => item.Path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(manifest.Files, item => item.Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    Assert.NotNull(archive.GetEntry("gafifish-backup-manifest.json"));
                    Assert.NotNull(archive.GetEntry("sessions.db"));
                    Assert.Null(archive.GetEntry("app-settings.json.tmp"));
                    Assert.Null(archive.GetEntry("old-backup.zip"));

                    var extractedDatabase = Path.Combine(directory, "database-check.db");
                    await using (var source = archive.GetEntry("sessions.db")!.Open())
                    await using (var destination = new FileStream(extractedDatabase, FileMode.Create, FileAccess.Write))
                    {
                        await source.CopyToAsync(destination);
                    }

                    var connectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = extractedDatabase,
                        Mode = SqliteOpenMode.ReadOnly
                    }.ToString();
                    await using var connection = new SqliteConnection(connectionString);
                    await connection.OpenAsync();
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT Value FROM BackupTest;";
                    Assert.Equal("snapshot-value", (string?)await command.ExecuteScalarAsync());
                }

                var verification = await service.VerifyBackupAsync(backupPath);
                Assert.True(verification.IsValid, verification.Message);
                Assert.Equal(manifest.Files.Count, verification.FileCount);
            }
            finally
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyBackup_RejectsTamperedFileContents()
    {
        var directory = CreateTemporaryDirectory();
        var backupPath = Path.Combine(Path.GetTempPath(), $"gafifish-tampered-{Guid.NewGuid():N}.zip");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "vision-profile.json"), "{ \"ok\": true }");
            var service = new UserDataBackupService(directory);
            await service.CreateBackupAsync(backupPath);

            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Update))
            {
                archive.GetEntry("vision-profile.json")!.Delete();
                var tampered = archive.CreateEntry("vision-profile.json");
                await using var stream = tampered.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync("modified after backup");
            }

            var verification = await service.VerifyBackupAsync(backupPath);
            Assert.False(verification.IsValid);
            Assert.Contains("SHA-256", verification.Message);
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyBackup_RejectsArchiveWithoutManifest()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"gafifish-no-manifest-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("some-file.txt");
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync("not a GafiFish backup");
            }

            var verification = await new UserDataBackupService().VerifyBackupAsync(backupPath);
            Assert.False(verification.IsValid);
            Assert.Contains("manifest", verification.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    private static async Task CreateDatabaseAsync(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE BackupTest (Value TEXT NOT NULL); INSERT INTO BackupTest(Value) VALUES ('snapshot-value');";
        await command.ExecuteNonQueryAsync();
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
