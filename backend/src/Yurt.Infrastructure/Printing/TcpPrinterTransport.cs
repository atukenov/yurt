using System.Net.Sockets;
using Yurt.Application.Common.Interfaces;

namespace Yurt.Infrastructure.Printing;

/// <summary>Raw printing over TCP (often called port 9100 or JetDirect printing): open a connection, write the bytes, close.</summary>
public class TcpPrinterTransport : IPrinterTransport
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task SendAsync(string host, int port, byte[] data, CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        using var client = new TcpClient { NoDelay = true };
        try
        {
            await client.ConnectAsync(host, port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(data, timeout.Token);
            await stream.FlushAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("The printer did not answer in time.");
        }
    }
}
