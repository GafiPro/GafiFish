using System.Security.Cryptography;
using System.Text;

namespace GafiFish.Ocr.Installation;

/// <summary>
/// Downloads the English fast traineddata from a pinned official
/// tesseract-ocr/tessdata_fast commit. The Git blob SHA-1 is checked before
/// the model is atomically installed under LocalAppData.
/// </summary>
public static class TesseractModelInstaller
{
    public const string ModelFileName = "eng.traineddata";
    public const int ExpectedModelSize = 4_113_088;
    public const string ExpectedGitBlobSha1 = "bbef4675053b5b468cdb477053e28b1c698ba08e";

    public const string ModelUrl =
        "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/87416418657359cb625c412a48b6e1d6d41c29bd/eng.traineddata";

    public static string DefaultDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GafiFish",
        "tessdata");

    public static async Task<string> EnsureEnglishModelAsync(
        HttpClient? httpClient = null,
        string? dataDirectory = null,
        CancellationToken cancellationToken = default)
    {
        var ownsClient = httpClient is null;
        httpClient ??= new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        try
        {
            var directory = dataDirectory ?? DefaultDataDirectory;
            Directory.CreateDirectory(directory);

            var modelPath = Path.Combine(directory, ModelFileName);
            if (await IsValidModelAsync(modelPath, cancellationToken))
            {
                return directory;
            }

            var response = await httpClient.GetAsync(
                ModelUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long length
                && length != ExpectedModelSize)
            {
                throw new InvalidDataException(
                    $"The English OCR model has an unexpected size ({length} bytes).");
            }

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var buffer = new MemoryStream(ExpectedModelSize);
            await input.CopyToAsync(buffer, cancellationToken);

            if (buffer.Length != ExpectedModelSize)
            {
                throw new InvalidDataException(
                    $"The English OCR model download is incomplete ({buffer.Length} bytes).");
            }

            var bytes = buffer.ToArray();
            VerifyGitBlobSha1(bytes, ExpectedGitBlobSha1);

            var temporaryPath = modelPath + ".download";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
                File.Move(temporaryPath, modelPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }

            return directory;
        }
        finally
        {
            if (ownsClient)
            {
                httpClient.Dispose();
            }
        }
    }

    public static void VerifyGitBlobSha1(ReadOnlySpan<byte> content, string expectedSha1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha1);

        var header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
        var blob = new byte[header.Length + content.Length];
        header.CopyTo(blob, 0);
        content.CopyTo(blob.AsSpan(header.Length));

        var actual = Convert.ToHexString(SHA1.HashData(blob)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actual),
                Encoding.ASCII.GetBytes(expectedSha1.ToLowerInvariant())))
        {
            throw new InvalidDataException("The downloaded OCR model failed its integrity check.");
        }
    }

    private static async Task<bool> IsValidModelAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var info = new FileInfo(path);
        if (info.Length != ExpectedModelSize)
        {
            return false;
        }

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 64 * 1024, useAsync: true);
        using var memory = new MemoryStream(ExpectedModelSize);
        await stream.CopyToAsync(memory, cancellationToken);

        try
        {
            VerifyGitBlobSha1(memory.ToArray(), ExpectedGitBlobSha1);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
