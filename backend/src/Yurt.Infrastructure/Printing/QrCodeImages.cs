using QRCoder;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Printing;

/// <summary>QR codes drawn with QRCoder as SVG (sharp on any printer), medium error correction like the ESC/POS one.</summary>
public class QrCodeImages : IQrCodeImages
{
    public string SvgDataUri(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data).GetGraphic(4);
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg))}";
    }
}
