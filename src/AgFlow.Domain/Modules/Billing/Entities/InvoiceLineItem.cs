namespace AgFlow.Domain.Modules.Billing.Entities;

public class InvoiceLineItem
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }

    private InvoiceLineItem() { }

    internal static InvoiceLineItem Create(Guid invoiceId, string description, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Line item description is required.", nameof(description));
        // الخصومات/الـ Credit Notes مش في الـ v1 — لحد ما نبنيها، المبلغ السالب يبقى غلط مش خصم
        if (amount < 0)
            throw new ArgumentException("Line item amount cannot be negative.", nameof(amount));

        return new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Description = description,
            Amount = amount
        };
    }
}
