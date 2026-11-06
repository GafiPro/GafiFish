using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace GafiFish.App.Backup;

public sealed record UserDataBackupFile(string Path, long SizeBytes, string Sha256);

public sealed record UserDataBackupManifest(
    string Product,
    int FormatVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<UserDataBackupFile> Files);

public sealed record UserDataBackupVerification(
    bool IsValid,
    int FileCount,
    long TotalBytes,
    string Message,
    UserDataBackupManifest? Manifest = null);

/// <summary>
/// Creates and verifies portable backups of GafiFish local data. SQLite is
/// snapshotted through SQLite's online backup API so an open sessions.db is safe.
/// No application binaries or game data are included.
/// </summary>
public sealed class UserDataBackupService
{
    private const string ProductName = "GafiFish";
    private const int CurrentFormatVersion = 1;
    private const string ManifestFileName = "gafifish-backup-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dataDirectory;

    public UserDataBackupService(string? dataDirectory = null)
    {
        _dataDirectory = Path.GetFullPath(dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductName));
    }

    public async Task<UserDataBackupManifest> CreateBackupAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        cancellationToken.ThrowIfCancellationRequested();

        var outputPath = Path.GetFullPath(destinationPath);
        var outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new ArgumentException("The backup destination must have a parent directory.", nameof(destinationPath));
        Directory.CreateDirectory(outputDirectory);

        var stagingDirectory = Path.Combine(Path.GetTempPath(), $"{ProductName}-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            if (Directory.Exists(_dataDirectory))
            {
                var sourceDatabase = Path.Combine(_dataDirectory, "sessions.db");
                if (File.Exists(sourceDatabase))
                {
                    var stagedDatabase = Path.Combine(stagingDirectory, "sessions.db");
                    await CreateDatabaseSnapshotAsync(sourceDatabase, stagedDatabase, cancellationToken);
                }

                foreach (var sourceFile in EnumerateSafeFiles(_dataDirectory)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var attributes = File.GetAttributes(sourceFile);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        continue;

                    var relativePath = Path.GetRelativePath(_dataDirectory, sourceFile);
                    if (!IsSafeRelativePath(relativePath)
                        || ShouldSkipFile(relativePath)
                        || string.Equals(Path.GetFullPath(sourceFile), outputPath, StringComparison.OrdinalIgnoreCase)
                        || IsSqliteDatabaseFile(relativePath))
                    {
                        continue;
                    }

                    var stagedPath = Path.Combine(stagingDirectory, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(stagedPath)!);
                    await CopyFileAsync(sourceFile, stagedPath, cancellationToken);
                }
            }

            var backupFiles = await BuildFileInventoryAsync(stagingDirectory, cancellationToken);
            var manifest = new UserDataBackupManifest(
                ProductName,
                CurrentFormatVersion,
                DateTimeOffset.UtcNow,
                backupFiles);

            var manifestPath = Path.Combine(stagingDirectory, ManifestFileName);
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken);

            await using (var output = new FileStream(
                outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 64 * 1024, useAsync: true))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories)
                             .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = Path.GetRelativePath(stagingDirectory, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);

                    await using var entryStream = entry.Open();
                    await using var sourceStream = new FileStream(
                        file, FileMode.Open, FileAccess.Read, FileShare.Read,
                        bufferSize: 64 * 1024, useAsync: true);
                    await sourceStream.CopyToAsync(entryStream, cancellationToken);
                }
            }

            return manifest;
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingDirectory))
                    Directory.Delete(stagingDirectory, recursive: true);
            }
            catch (IOException)
            {
                // A failed temporary cleanup must not hide the actual backup result.
            }
            catch (UnauthorizedAccessException)
            {
                // A failed temporary cleanup must not hide the actual backup result.
            }
        }
    }

    public async Task<UserDataBackupVerification> VerifyBackupAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var input = new FileStream(
                archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);

            var manifestEntries = archive.Entries
                .Where(entry => string.Equals(entry.FullName, ManifestFileName, StringComparison.Ordinal))
                .ToArray();
            if (manifestEntries.Length != 1)
                return Invalid("The backup must contain exactly one GafiFish manifest.");

            UserDataBackupManifest? manifest;
            await using (var manifestStream = manifestEntries[0].Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<UserDataBackupManifest>(
                    manifestStream, JsonOptions, cancellationToken);
            }

            if (manifest is null
                || !string.Equals(manifest.Product, ProductName, StringComparison.Ordinal)
                || manifest.FormatVersion != CurrentFormatVersion
                || manifest.Files is null)
            {
                return Invalid("The backup manifest is missing, unsupported or invalid.");
            }

            var expectedFiles = new Dictionary<string, UserDataBackupFile>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.Files)
            {
                if (file is null
                    || !IsSafeArchivePath(file.Path)
                    || string.Equals(file.Path, ManifestFileName, StringComparison.OrdinalIgnoreCase)
                    || file.SizeBytes < 0
                    || string.IsNullOrWhiteSpace(file.Sha256)
                    || !expectedFiles.TryAdd(file.Path, file))
                {
                    return Invalid("The backup manifest contains an unsafe or duplicate file entry.");
                }
            }

            var archiveFiles = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, ManifestFileName, StringComparison.Ordinal))
                    continue;
                if (!IsSafeArchivePath(entry.FullName) || !archiveFiles.TryAdd(entry.FullName, entry))
                    return Invalid("The archive contains an unsafe or duplicate file path.");
            }

            if (archiveFiles.Count != expectedFiles.Count)
                return Invalid("The archive contents do not match the file list in its manifest.");

            long totalBytes = 0;
            foreach (var expected in expectedFiles.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!archiveFiles.TryGetValue(expected.Path, out var entry))
                    return Invalid($"A backup file is missing: {expected.Path}");

                if (entry.Length != expected.SizeBytes)
                    return Invalid($"A backup file has an unexpected size: {expected.Path}");

                await using var stream = entry.Open();
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
                if (!string.Equals(hash, expected.Sha256, StringComparison.OrdinalIgnoreCase))
                    return Invalid($"A backup file failed its SHA-256 check: {expected.Path}");

                totalBytes += entry.Length;
            }

            return new UserDataBackupVerification(
                true,
                expectedFiles.Count,
                totalBytes,
                $"Backup valid · {expectedFiles.Count:N0} files · SHA-256 checks passed.",
                manifest);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            return Invalid($"Could not verify backup: {exception.Message}");
        }
    }

    private static IEnumerable<string> EnumerateSafeFiles(string rootDirectory)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectory);

        while (pendingDirectories.Count > 0)
        {
            var currentDirectory = pendingDirectories.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(currentDirectory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                if ((attributes & FileAttributes.Directory) != 0)
                    pendingDirectories.Push(entry);
                else
                    yield return entry;
            }
        }
    }

    private static async Task CreateDatabaseSnapshotAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false
            }.ToString();
            var destinationConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString();

            using var source = new SqliteConnection(sourceConnectionString);
            using var destination = new SqliteConnection(destinationConnectionString);
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }, cancellationToken);
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024, useAsync: true);
        await using var destination = new FileStream(
            destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 64 * 1024, useAsync: true);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static async Task<IReadOnlyList<UserDataBackupFile>> BuildFileInventoryAsync(
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        var files = new List<UserDataBackupFile>();
        foreach (var path in Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(stagingDirectory, path)
                .Replace(Path.DirectorySeparatorChar, '/');
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 64 * 1024, useAsync: true);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            files.Add(new UserDataBackupFile(relativePath, stream.Length, hash));
        }

        return files;
    }

    private static bool ShouldSkipFile(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSqliteDatabaseFile(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return string.Equals(fileName, "sessions.db", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("sessions.db-", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("sessions.db.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeRelativePath(string relativePath) =>
        !string.IsNullOrWhiteSpace(relativePath)
        && !Path.IsPathRooted(relativePath)
        && !relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])
            .Any(segment => segment is "" or "." or "..");

    private static bool IsSafeArchivePath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && !path.StartsWith("/", StringComparison.Ordinal)
        && !path.Contains('\\')
        && !path.Contains(':')
        && !path.Split('/').Any(segment => segment is "" or "." or "..");

    private static UserDataBackupVerification Invalid(string message) =>
        new(false, 0, 0, message);
}
