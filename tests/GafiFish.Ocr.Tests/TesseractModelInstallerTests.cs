using GafiFish.Ocr.Installation;
using Xunit;

namespace GafiFish.Ocr.Tests;

public sealed class TesseractModelInstallerTests
{
    [Fact]
    public void VerifyGitBlobSha1_RejectsUnexpectedHash()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };

        Assert.Throws<InvalidDataException>(
            () => TesseractModelInstaller.VerifyGitBlobSha1(data, "0000000000000000000000000000000000000000"));
    }

    [Fact]
    public void ModelMetadata_UsesPinnedEnglishModel()
    {
        Assert.Equal("eng.traineddata", TesseractModelInstaller.ModelFileName);
        Assert.Equal(4_113_088, TesseractModelInstaller.ExpectedModelSize);
        Assert.Equal(40, TesseractModelInstaller.ExpectedGitBlobSha1.Length);
        Assert.Contains("87416418657359cb625c412a48b6e1d6d41c29bd", TesseractModelInstaller.ModelUrl);
    }
}
