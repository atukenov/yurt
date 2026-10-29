using System.Text.Json;
using Yurt.Application.Features.Billing;
using Yurt.Infrastructure.Payments;

namespace Yurt.UnitTests;

/// <summary>Reading the platform billing webhooks in the older and the 2025 Stripe API shapes.</summary>
public class StripeBillingGatewayTests
{
    private static BillingEvent? Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return StripeBillingGateway.ReadEvent(doc.RootElement);
    }

    [Fact]
    public void InvoicePaid_OlderShape_ReadsSubscriptionMetadataAndLinePeriod()
    {
        var cafe = Guid.NewGuid();
        var e = Read($$"""
        { "id": "evt_1", "type": "invoice.paid", "data": { "object": {
            "id": "in_1", "customer": "cus_1", "subscription": "sub_1", "amount_paid": 1500000, "period_end": 1700000000,
            "subscription_details": { "metadata": { "tenant_id": "{{cafe}}" } },
            "lines": { "data": [ { "period": { "start": 1700000000, "end": 1702592000 } } ] } } } }
        """)!;

        Assert.Equal(BillingEventKind.InvoicePaid, e.Kind);
        Assert.Equal(cafe, e.TenantId);
        Assert.Equal("sub_1", e.SubscriptionId);
        Assert.Equal("cus_1", e.CustomerId);
        Assert.Equal(15000m, e.Amount);
        // The line's period (the month paid for), not the invoice's own period_end.
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1702592000).UtcDateTime, e.PeriodEnd);
    }

    [Fact]
    public void InvoicePaid_NewShape_ReadsParentSubscriptionDetails()
    {
        var e = Read("""
        { "id": "evt_2", "type": "invoice.paid", "data": { "object": {
            "id": "in_2", "customer": { "id": "cus_2" }, "amount_paid": 4900,
            "parent": { "type": "subscription_details", "subscription_details": { "subscription": "sub_2", "metadata": {} } },
            "lines": { "data": [ { "period": { "end": 1702592000 } } ] } } } }
        """)!;

        Assert.Equal("sub_2", e.SubscriptionId);
        Assert.Equal("cus_2", e.CustomerId);
        Assert.Null(e.TenantId);
    }

    [Fact]
    public void OneOffInvoiceAndPaymentModeCheckout_AreIgnored()
    {
        Assert.Null(Read("""{ "id": "evt_3", "type": "invoice.paid", "data": { "object": { "id": "in_3", "customer": "cus_3", "amount_paid": 100 } } }"""));
        Assert.Null(Read("""{ "id": "evt_4", "type": "checkout.session.completed", "data": { "object": { "id": "cs_1", "mode": "payment" } } }"""));
    }

    [Fact]
    public void SubscriptionDeleted_ReadsAsCanceled()
    {
        var e = Read("""
        { "id": "evt_5", "type": "customer.subscription.deleted", "data": { "object": {
            "id": "sub_5", "customer": "cus_5", "status": "canceled", "cancel_at_period_end": false, "metadata": {} } } }
        """)!;
        Assert.Equal(BillingEventKind.SubscriptionChanged, e.Kind);
        Assert.Equal("canceled", e.Status);
        Assert.Equal("sub_5", e.SubscriptionId);
    }
}
