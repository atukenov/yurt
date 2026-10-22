using System.Text;
using Yurt.Infrastructure.Printing;

namespace Yurt.UnitTests;

/// <summary>The QR picture on the receipt printed from the browser.</summary>
public class QrCodeImagesTests
{
    [Fact]
    public void SvgDataUri_IsAnSvgPicture()
    {
        const string prefix = "data:image/svg+xml;base64,";
        var uri = new QrCodeImages().SvgDataUri("https://ofd.example/t/1?i=123&f=456");
        Assert.StartsWith(prefix, uri);
        var svg = Encoding.UTF8.GetString(Convert.FromBase64String(uri[prefix.Length..]));
        Assert.Contains("<svg", svg);
    }
}
