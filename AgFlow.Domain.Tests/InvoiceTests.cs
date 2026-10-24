using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Billing.Entities;
using AgFlow.Domain.Modules.Billing.Events;
using Xunit;

namespace AgFlow.Domain.Tests;

public class InvoiceTests
{
    private static Invoice NewDraft() =>
        Invoice.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Acme Co");

    [Fact]
    public void CreateDraft_StartsAsDraft_WithClientNameSnapshot()
    {
        var invoice = NewDraft();

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal("Acme Co", invoice.ClientNameSnapshot);
        Assert.Equal(0m, invoice.TotalAmount);
    }

    [Fact]
    public void CreateDraft_WithOnlyOneSideOfPeriod_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Invoice.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Acme", Guid.NewGuid(),
                periodStart: new DateTime(2026, 9, 1), periodEnd: null));
    }

    [Fact]
    public void CreateDraft_WithPeriodEndNotAfterStart_Throws()
    {
        var start = new DateTime(2026, 9, 1);
        Assert.Throws<ArgumentException>(() =>
            Invoice.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Acme", Guid.NewGuid(), start, start));
    }

    [Fact]
    public void TotalAmount_IsSumOfLineItems()
    {
        var invoice = NewDraft();
        invoice.AddLineItem("Retainer", 2000m);
        invoice.AddLineItem("Approved overage", 150.50m);

        Assert.Equal(2150.50m, invoice.TotalAmount);
    }

    [Fact]
    public void AddLineItem_AfterSent_Throws()
    {
        var invoice = NewDraft();
        invoice.AddLineItem("Retainer", 2000m);
        invoice.MarkAsSent();

        Assert.Throws<DomainException>(() => invoice.AddLineItem("Sneaky", 500m));
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("   ", 100)]
    [InlineData("Retainer", -1)]
    public void AddLineItem_WithInvalidData_Throws(string description, decimal amount)
    {
        var invoice = NewDraft();
        Assert.Throws<ArgumentException>(() => invoice.AddLineItem(description, amount));
    }

    [Fact]
    public void MarkAsSent_Twice_Throws()
    {
        var invoice = NewDraft();
        invoice.MarkAsSent();

        Assert.Throws<DomainException>(() => invoice.MarkAsSent());
    }

    [Fact]
    public void MarkAsPaid_FromDraft_Throws()
    {
        // دورة الحياة (FR-BI-5): فاتورة ماتبعتتش مينفعش تتدفع
        var invoice = NewDraft();
        invoice.AddLineItem("Retainer", 2000m);

        Assert.Throws<DomainException>(() => invoice.MarkAsPaid());
    }

    [Fact]
    public void MarkAsPaid_AfterSent_SetsPaidAndRaisesOneEventWithTotal()
    {
        var invoice = NewDraft();
        invoice.AddLineItem("Retainer", 2000m);
        invoice.MarkAsSent();

        invoice.MarkAsPaid();

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.NotNull(invoice.PaidAt);
        var paidEvent = Assert.Single(invoice.DomainEvents);
        var typed = Assert.IsType<InvoicePaidEvent>(paidEvent);
        Assert.Equal(invoice.Id, typed.InvoiceId);
        Assert.Equal(2000m, typed.Amount);
    }

    [Fact]
    public void MarkAsPaid_Twice_DoesNotRaiseSecondEvent()
    {
        // لو Event التاني اتطلع، العميل هيستلم إيميل "تم الدفع" مرتين
        var invoice = NewDraft();
        invoice.AddLineItem("Retainer", 2000m);
        invoice.MarkAsSent();

        invoice.MarkAsPaid();
        invoice.MarkAsPaid();

        Assert.Single(invoice.DomainEvents);
    }
}
