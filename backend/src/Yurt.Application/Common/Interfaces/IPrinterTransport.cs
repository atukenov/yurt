namespace Yurt.Application.Common.Interfaces;

/// <summary>Sends raw bytes to a network printer (own_pos). Throws when the printer cannot be reached.</summary>
public interface IPrinterTransport
{
    Task SendAsync(string host, int port, byte[] data, CancellationToken ct = default);
}
