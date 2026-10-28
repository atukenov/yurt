using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.NSubstitute;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Payments.DTOs;
using Yurt.Application.Features.Payments.Services;
using Yurt.Domain;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;

namespace Yurt.UnitTests;

/// <summary>When the customer app is offered "pay online", and what happens when a cafe has not set it up.
/// The full Stripe flow (page, webhooks, refunds) is covered by the integration tests.</summary>
public class PaymentServiceTests
{
    private static readonly Guid CafeId = Guid.NewGuid();

    private static (PaymentService Svc, IPaymentProvider Stripe) Build(
        string? cafeProvider, bool moduleOn = true, bool keysSet = true, IEnumerable<Order>? orders = null)
    {
        var db = Substitute.For<IApplicationDbContext>();
        var cafe = new Tenant { Id = CafeId, Slug = "cafe", Name = "Cafe", PaymentProvider = cafeProvider };
        var tenants = new List<Tenant> { cafe }.BuildMockDbSet();
        var orderSet = (orders ?? []).ToList().BuildMockDbSet();
        var payments = new List<Payment>().BuildMockDbSet();
        db.Tenants.Returns(tenants);
        db.Orders.Returns(orderSet);
        db.Payments.Returns(payments);

        var tenant = Substitute.For<ITenantContext>();
        tenant.TenantId.Returns(CafeId);
        tenant.Slug.Returns("cafe");

        var modules = Substitute.For<IModuleGate>();
        modules.IsEnabledAsync(ModuleKeys.OnlinePayments, Arg.Any<CancellationToken>()).Returns(moduleOn);

        var stripe = Substitute.For<IPaymentProvider>();
        stripe.Provider.Returns(PaymentProvider.Stripe);
        stripe.IsConfiguredAsync(Arg.Any<CancellationToken>()).Returns(keysSet);
        var factory = Substitute.For<IPaymentProviderFactory>();
        factory.GetProvider(PaymentProvider.Stripe).Returns(stripe);

        var svc = new PaymentService(db, Substitute.For<IOrdersHubService>(), factory, tenant, modules,
            Substitute.For<IAuditLogService>(), new PaymentReturnOptions(), NullLogger<PaymentService>.Instance);
        return (svc, stripe);
    }

    [Fact]
    public async Task Config_IsStripe_OnlyWhenTheModuleIsOn_StripeIsChosen_AndItsKeysAreSet()
    {
        Assert.Equal("stripe", (await Build("stripe").Svc.GetConfigAsync()).Provider);
        Assert.Null((await Build("stripe", moduleOn: false).Svc.GetConfigAsync()).Provider);
        Assert.Null((await Build("stripe", keysSet: false).Svc.GetConfigAsync()).Provider);
        Assert.Null((await Build(null).Svc.GetConfigAsync()).Provider);
    }

    [Fact]
    public async Task CreatePayment_WhenTheCafeTakesNoOnlinePayments_IsRefused_WithoutCallingStripe()
    {
        var customer = Guid.NewGuid();
        var order = new Order { CustomerUserId = customer, Status = OrderStatus.Created, Total = 1500 };
        var (svc, stripe) = Build(null, orders: [order]);

        var result = await svc.CreatePaymentAsync(customer, new CreatePaymentDto(order.Id));

        Assert.False(result.Succeeded);
        Assert.Equal(422, result.StatusCode);
        await stripe.DidNotReceive().CreateInvoiceAsync(Arg.Any<CreatePaymentInvoiceRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePayment_ForAnAcceptedOrSomeoneElsesOrder_IsRefused()
    {
        var customer = Guid.NewGuid();
        var accepted = new Order { CustomerUserId = customer, Status = OrderStatus.Accepted, Total = 1500 };
        var (svc, _) = Build("stripe", orders: [accepted]);

        Assert.Equal(422, (await svc.CreatePaymentAsync(customer, new CreatePaymentDto(accepted.Id))).StatusCode);
        Assert.Equal(403, (await svc.CreatePaymentAsync(Guid.NewGuid(), new CreatePaymentDto(accepted.Id))).StatusCode);
        Assert.Equal(404, (await svc.CreatePaymentAsync(customer, new CreatePaymentDto(Guid.NewGuid()))).StatusCode);
    }
}
