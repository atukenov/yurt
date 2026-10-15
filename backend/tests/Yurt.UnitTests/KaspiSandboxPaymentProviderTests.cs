using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Payments;

namespace Yurt.UnitTests;

/// <summary>
/// Tests for <see cref="KaspiSandboxPaymentProvider"/> — the mock provider that
/// stands in for real Kaspi Pay today. The webhook-simulation delay is pushed
/// far out (matching the trick <c>YurtWebAppFactory</c> uses for integration
/// tests) so assertions observe the invoice in its initial "Pending" state.
/// </summary>
public class KaspiSandboxPaymentProviderTests
{
    private static KaspiSandboxPaymentProvider Build()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        var options = Options.Create(new PaymentOptions
        {
            SandboxBaseUrl = "https://kaspi.sandbox.example",
            WebhookCallbackUrl = "http://test.invalid/webhook",
            WebhookSecret = "secret",
            // Never actually fires during the test.
            ProcessingDelaySecondsMin = 99999,
            ProcessingDelaySecondsMax = 99999,
        });
        var logger = Substitute.For<ILogger<KaspiSandboxPaymentProvider>>();
        return new KaspiSandboxPaymentProvider(httpClientFactory, options, logger);
    }

    private static CreatePaymentInvoiceRequest Request(
        SandboxPaymentBehavior behavior = SandboxPaymentBehavior.Success) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 2500m, Currency.KZT, PaymentProvider.KaspiSandbox, behavior);

    [Fact]
    public void Provider_IsKaspiSandbox()
    {
        Assert.Equal(PaymentProvider.KaspiSandbox, Build().Provider);
    }

    [Fact]
    public async Task CreateInvoiceAsync_ReturnsPendingInvoiceWithExpectedShape()
    {
        var provider = Build();

        var response = await provider.CreateInvoiceAsync(Request());

        Assert.StartsWith("KS-", response.InvoiceId);
        Assert.Equal(PaymentRecordStatus.Pending, response.Status);
        Assert.Equal(2500m, response.Amount);
        Assert.Equal(Currency.KZT, response.Currency);
        Assert.Equal(PaymentProvider.KaspiSandbox, response.Provider);
        Assert.Contains(response.InvoiceId, response.PaymentUrl);
        Assert.StartsWith("data:image/png;base64,", response.QrCode);
        Assert.NotNull(response.ExpiresAt);
        Assert.True(response.ExpiresAt > response.CreatedAt);
    }

    [Fact]
    public async Task CreateInvoiceAsync_EachInvoiceGetsAUniqueId()
    {
        var provider = Build();

        var first = await provider.CreateInvoiceAsync(Request());
        var second = await provider.CreateInvoiceAsync(Request());

        Assert.NotEqual(first.InvoiceId, second.InvoiceId);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_UnknownInvoiceId_Throws()
    {
        var provider = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetPaymentStatusAsync("does-not-exist"));
    }

    [Fact]
    public async Task GetPaymentStatusAsync_JustCreatedInvoice_IsPending()
    {
        var provider = Build();
        var created = await provider.CreateInvoiceAsync(Request());

        var status = await provider.GetPaymentStatusAsync(created.InvoiceId);

        Assert.Equal(PaymentRecordStatus.Pending, status.Status);
        Assert.Equal(created.InvoiceId, status.InvoiceId);
        Assert.Equal(2500m, status.Amount);
    }
}
