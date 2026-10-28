using Yurt.Application.Features.Pictures;

namespace Yurt.UnitTests;

public class PictureSniffTests
{
    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    public void KnownPictures_AreRecognised(byte[] head, string expected) => Assert.Equal(expected, PictureService.Sniff(head));

    [Theory]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 })] // "<svg"
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 })] // "GIF8"
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x41, 0x56, 0x45 })] // RIFF WAVE
    [InlineData(new byte[] { })]
    public void AnythingElse_IsRefused(byte[] head) => Assert.Null(PictureService.Sniff(head));
}
