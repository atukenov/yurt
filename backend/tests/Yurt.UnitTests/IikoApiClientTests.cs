using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Yurt.Application.Features.Loyalty;
using Yurt.Infrastructure.Iiko;

namespace Yurt.UnitTests;

/// <summary>
/// Regression coverage for the wallet-id bug: iiko's customer/program/add returns both
/// userWalletId (the customer's own balance-holding wallet) and walletId (the shared,
/// non-balance-holding program wallet definition). We must always resolve to
/// userWalletId — using walletId instead makes every balance lookup and wallet
/// operation target a wallet the customer doesn't actually hold points in.
/// </summary>
public class IikoApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Guid _userWalletId;
        private readonly Guid _walletId;

        public StubHandler(Guid userWalletId, Guid walletId)
        {
            _userWalletId = userWalletId;
            _walletId = walletId;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("access_token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { correlationId = "c1", token = "fake-token" })
                });
            }

            if (request.RequestUri.AbsolutePath.EndsWith("customer/program/add"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { userWalletId = _userWalletId, walletId = _walletId })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task AddCustomerToProgramAsync_PrefersUserWalletId_OverSharedProgramWalletId()
    {
        var userWalletId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var programWalletId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var httpClient = new HttpClient(new StubHandler(userWalletId, programWalletId));
        var options = new IikoOptions
        {
            Enabled = true,
            BaseUrl = "https://iiko.test",
            ApiKey = "key",
            AppId = "app",
            ClientSecret = "secret",
            OrganizationId = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
        };
        var client = new IikoApiClient(
            httpClient, options, new IikoTokenStore(), NullLogger<IikoApiClient>.Instance);

        var resolved = await client.AddCustomerToProgramAsync(Guid.NewGuid());

        Assert.Equal(userWalletId, resolved);
        Assert.NotEqual(programWalletId, resolved);
    }

    private sealed class TransactionsStubHandler : HttpMessageHandler
    {
        private readonly Guid _transactionId;
        private readonly Guid _posOrderId;

        public TransactionsStubHandler(Guid transactionId, Guid posOrderId)
        {
            _transactionId = transactionId;
            _posOrderId = posOrderId;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("access_token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { correlationId = "c1", token = "fake-token" })
                });
            }

            if (request.RequestUri.AbsolutePath.EndsWith("customer/transactions/by_date"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        transactions = new[]
                        {
                            new
                            {
                                id = _transactionId,
                                whenCreated = "2026-01-15 10:30:00.000",
                                sum = 150m,
                                orderSum = 1500m,
                                orderNumber = 42,
                                posOrderId = _posOrderId,
                                typeName = "Bonus payment",
                                isDelivery = false,
                                balanceBefore = 100m,
                                balanceAfter = 250m,
                                comment = (string?)null,
                            }
                        }
                    })
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task GetCustomerTransactionsAsync_MapsOffsiteTransactionFields()
    {
        var iikoCustomerId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var posOrderId = Guid.NewGuid();

        var httpClient = new HttpClient(new TransactionsStubHandler(transactionId, posOrderId));
        var options = new IikoOptions
        {
            Enabled = true,
            BaseUrl = "https://iiko.test",
            ApiKey = "key",
            AppId = "app",
            ClientSecret = "secret",
            OrganizationId = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
        };
        var client = new IikoApiClient(
            httpClient, options, new IikoTokenStore(), NullLogger<IikoApiClient>.Instance);

        var result = await client.GetCustomerTransactionsAsync(
            iikoCustomerId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));

        var tx = Assert.Single(result);
        Assert.Equal(transactionId, tx.Id);
        Assert.Equal(150m, tx.Sum);
        Assert.Equal(42, tx.OrderNumber);
        Assert.Equal(posOrderId, tx.PosOrderId);
        Assert.Equal("Bonus payment", tx.TypeName);
        Assert.Equal(250m, tx.BalanceAfter);
        Assert.Null(tx.Comment);
    }
}
