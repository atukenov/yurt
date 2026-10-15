using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Payments;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Application.Features.Payments.Services;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

public class PaymentServiceTests
{
    private static (PaymentService svc, IApplicationDbContext db, IOrdersHubService hub) Build(
        IEnumerable<Payment>? payments = null, bool freedomPayEnabled = true)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var set = (payments ?? []).ToList().BuildMockDbSet();
        db.Payments.Returns(set);

        var hub = Substitute.For<IOrdersHubService>();
        var providerFactory = Substitute.For<IPaymentProviderFactory>();
        var webhookValidator = Substitute.For<IPaymentWebhookValidator>();
        var options = new FreedomPayOptions { Enabled = freedomPayEnabled, AppleMerchantId = "merchant.com.yurt.app" };
        var logger = Substitute.For<ILogger<PaymentService>>();

        var svc = new PaymentService(db, hub, providerFactory, webhookValidator, options, logger);
        return (svc, db, hub);
    }

    private static Payment MakePayment(
        Guid customerId, PaymentRecordStatus status = PaymentRecordStatus.Pending, string invoiceId = "FP-1")
    {
        var order = new Order
        {
            CustomerUserId = customerId,
            Status = OrderStatus.Created,
            PaymentStatus = PaymentStatus.Unpaid,
        };
        return new Payment
        {
            OrderId = order.Id,
            Order = order,
            Provider = PaymentProvider.FreedomPay,
            InvoiceId = invoiceId,
            Amount = 1500,
            Status = status,
            PaymentUrl = "",
            QrCode = "",
        };
    }

    [Fact]
    public void GetApplePayConfig_ReturnsOptionsValues()
    {
        var (svc, _, _) = Build(freedomPayEnabled: true);

        var config = svc.GetApplePayConfig();

        Assert.True(config.Enabled);
        Assert.Equal("merchant.com.yurt.app", config.AppleMerchantId);
    }

    [Fact]
    public async Task ConfirmApplePayAsync_Disabled_ReturnsFailure()
    {
        var customerId = Guid.NewGuid();
        var payment = MakePayment(customerId);
        var (svc, _, hub) = Build([payment], freedomPayEnabled: false);

        var result = await svc.ConfirmApplePayAsync(customerId, new ConfirmApplePayDto(payment.InvoiceId, "fp-123"));

        Assert.False(result.Succeeded);
        Assert.Equal(422, result.StatusCode);
        await hub.DidNotReceive().NotifyPaymentSucceededAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmApplePayAsync_InvoiceNotFound_ReturnsNotFound()
    {
        var (svc, _, _) = Build();

        var result = await svc.ConfirmApplePayAsync(Guid.NewGuid(), new ConfirmApplePayDto("missing", "fp-123"));

        Assert.False(result.Succeeded);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task ConfirmApplePayAsync_WrongCustomer_ReturnsForbidden()
    {
        var payment = MakePayment(Guid.NewGuid());
        var (svc, _, _) = Build([payment]);

        var result = await svc.ConfirmApplePayAsync(Guid.NewGuid(), new ConfirmApplePayDto(payment.InvoiceId, "fp-123"));

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task ConfirmApplePayAsync_AlreadyPaid_IsIdempotentSuccess()
    {
        var customerId = Guid.NewGuid();
        var payment = MakePayment(customerId, PaymentRecordStatus.Paid);
        var (svc, db, hub) = Build([payment]);

        var result = await svc.ConfirmApplePayAsync(customerId, new ConfirmApplePayDto(payment.InvoiceId, "fp-123"));

        Assert.True(result.Succeeded);
        await db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await hub.DidNotReceive().NotifyPaymentSucceededAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmApplePayAsync_AlreadyFailed_ReturnsConflict()
    {
        var customerId = Guid.NewGuid();
        var payment = MakePayment(customerId, PaymentRecordStatus.Failed);
        var (svc, _, _) = Build([payment]);

        var result = await svc.ConfirmApplePayAsync(customerId, new ConfirmApplePayDto(payment.InvoiceId, "fp-123"));

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task ConfirmApplePayAsync_Pending_MarksPaidAndTransitionsOrder()
    {
        var customerId = Guid.NewGuid();
        var payment = MakePayment(customerId);
        var (svc, db, hub) = Build([payment]);

        var result = await svc.ConfirmApplePayAsync(customerId, new ConfirmApplePayDto(payment.InvoiceId, "fp-123"));

        Assert.True(result.Succeeded);
        Assert.Equal(PaymentRecordStatus.Paid, payment.Status);
        Assert.Equal(PaymentStatus.Paid, payment.Order.PaymentStatus);
        Assert.Equal(OrderStatus.Preparing, payment.Order.Status);
        await db.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await hub.Received(1).NotifyPaymentSucceededAsync(payment.Order, Arg.Any<CancellationToken>());
    }
}
