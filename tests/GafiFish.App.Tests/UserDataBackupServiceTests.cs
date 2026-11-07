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
            await CreateDatabaseAsync(Path.Combine(directory, "sessions.db"), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "vision-profile.json"), "{ \"profile\": 1 }", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "app-settings.json"), "{ \"hotkey\": \"F8\" }", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "rod-profile-bindings.json"), "{ \"SlotProfiles\": { \"1\": \"Rod Alpha\" } }", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "app-settings.json.tmp"), "temporary", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "old-backup.zip"), "do not nest old backups", TestContext.Current.CancellationToken);
            Directory.CreateDirectory(Path.Combine(directory, "logs"));
            await File.WriteAllTextAsync(Path.Combine(directory, "logs", "activity.log"), "INFO saved", TestContext.Current.CancellationToken);

            var backupPath = Path.Combine(Path.GetTempPath(), $"gafifish-backup-{Guid.NewGuid():N}.zip");
            try
            {
                var service = new UserDataBackupService(directory);
                var manifest = await service.CreateBackupAsync(backupPath, TestContext.Current.CancellationToken);

                Assert.Contains(manifest.Files, item => item.Path == "sessions.db");
                Assert.Contains(manifest.Files, item => item.Path == "vision-profile.json");
                Assert.Contains(manifest.Files, item => item.Path == "rod-profile-bindings.json");
                Assert.Contains(manifest.Files, item => item.Path == "logs/activity.log");
                Assert.DoesNotContain(manifest.Files, item => item.Path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(manifest.Files, item => item.Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    Assert.NotNull(archive.GetEntry("gafifish-backup-manifest.json"));
                    Assert.NotNull(archive.GetEntry("sessions.db"));
                    Assert.NotNull(archive.GetEntry("rod-profile-bindings.json"));
                    Assert.Null(archive.GetEntry("app-settings.json.tmp"));
                    Assert.Null(archive.GetEntry("old-backup.zip"));

                    var extractedDatabase = Path.Combine(directory, "database-check.db");
                    await using (var source = archive.GetEntry("sessions.db")!.Open())
                    await using (var destination = new FileStream(extractedDatabase, FileMode.Create, FileAccess.Write))
                    {
                        await source.CopyToAsync(destination, TestContext.Current.CancellationToken);
                    }

                    var connectionString = new SqliteConnectionStringBuilder
                    {
                        DataSource = extractedDatabase,
                        Mode = SqliteOpenMode.ReadOnly,
                        Pooling = false
                    }.ToString();
                    await using (var connection = new SqliteConnection(connectionString))
                    {
                        await connection.OpenAsync(TestContext.Current.CancellationToken);
                        await using var command = connection.CreateCommand();
                        command.CommandText = "SELECT Value FROM BackupTest;";
                        Assert.Equal("snapshot-value", (string?)await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
                    }
                }

                var verification = await service.VerifyBackupAsync(backupPath, TestContext.Current.CancellationToken);
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
    public async Task VerifyBackup_ReturnsManifestForPreviewWithoutExtractingFiles()
    {
        var directory = CreateTemporaryDirectory();
        var backupPath = Path.Combine(Path.GetTempPath(), $"gafifish-preview-{Guid.NewGuid():N}.zip");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "vision-profile.json"), "{ \"profile\": 2 }", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(directory, "app-settings.json"), "{ \"key\": \"F8\" }", TestContext.Current.CancellationToken);

            var service = new UserDataBackupService(directory);
            var created = await service.CreateBackupAsync(backupPath, TestContext.Current.CancellationToken);
            var inspected = await service.VerifyBackupAsync(backupPath, TestContext.Current.CancellationToken);

            Assert.True(inspected.IsValid, inspected.Message);
            Assert.NotNull(inspected.Manifest);
            Assert.Equal(created.Files.Count, inspected.Manifest.Files.Count);
            Assert.Contains(inspected.Manifest.Files, file => file.Path == "vision-profile.json");
            Assert.Contains(inspected.Manifest.Files, file => file.Path == "app-settings.json");
            Assert.False(File.Exists(Path.Combine(directory, "gafifish-backup-manifest.json")));
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
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
            await File.WriteAllTextAsync(Path.Combine(directory, "vision-profile.json"), "original-text", TestContext.Current.CancellationToken);
            var service = new UserDataBackupService(directory);
            await service.CreateBackupAsync(backupPath, TestContext.Current.CancellationToken);

            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Update))
            {
                archive.GetEntry("vision-profile.json")!.Delete();
                var tampered = archive.CreateEntry("vision-profile.json");
                await using var stream = tampered.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync("tampered-text".AsMemory(), TestContext.Current.CancellationToken);
            }

            var verification = await service.VerifyBackupAsync(backupPath, TestContext.Current.CancellationToken);
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
                await writer.WriteAsync("not a GafiFish backup".AsMemory(), TestContext.Current.CancellationToken);
            }

            var verification = await new UserDataBackupService().VerifyBackupAsync(backupPath, TestContext.Current.CancellationToken);
            Assert.False(verification.IsValid);
            Assert.Contains("manifest", verification.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
    }

    private static async Task CreateDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE BackupTest (Value TEXT NOT NULL); INSERT INTO BackupTest(Value) VALUES ('snapshot-value');";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gafifish-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
