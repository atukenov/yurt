namespace Yurt.Application.Common.Interfaces;

/// <summary>Draws QR codes as pictures, for receipts printed from the browser (own_pos).</summary>
public interface IQrCodeImages
{
    /// <summary>The QR code of <paramref name="text"/> as an SVG picture in a <c>data:image/svg+xml;base64,</c> link.</summary>
    string SvgDataUri(string text);
}
