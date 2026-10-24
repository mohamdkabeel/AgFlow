using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Billing.Events;

namespace AgFlow.Domain.Modules.Billing.Entities;

public enum InvoiceStatus
{
    Draft,
    Sent,
    Viewed,
    Paid,
    Overdue,
    Void
}

public class Invoice : ITenantScoped, IHasDomainEvents
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ClientId { get; private set; }

    // الفاتورة الشهرية بتعرف نفسها: "الـ Retainer ده، عن الفترة دي".
    // ده اللي بيسمح بمنع التكرار (Unique Index في الـ Configuration).
    // Nullable لأن فواتير يدوية (مش من Retainer) ممكن تتعمل لاحقاً.
    public Guid? RetainerAgreementId { get; private set; }
    public DateTime? PeriodStart { get; private set; } // شامل
    public DateTime? PeriodEnd { get; private set; }   // غير شامل (أول يوم في الشهر اللي بعده)

    // Snapshot لاسم العميل وقت الإصدار (Phase 5, Step 4)
    public string ClientNameSnapshot { get; private set; } = string.Empty;

    public InvoiceStatus Status { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime? PaidAt { get; private set; }

    private readonly List<InvoiceLineItem> _lineItems = new();
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    public decimal TotalAmount => _lineItems.Sum(li => li.Amount);

    private Invoice() { }

    public static Invoice CreateDraft(
        Guid workspaceId, Guid clientId, string clientNameSnapshot,
        Guid? retainerAgreementId = null, DateTime? periodStart = null, DateTime? periodEnd = null)
    {
        if (periodStart.HasValue != periodEnd.HasValue)
            throw new ArgumentException("Provide both period start and end, or neither.");
        if (periodStart.HasValue && periodEnd <= periodStart)
            throw new ArgumentException("Period end must be after period start.");

        return new Invoice
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClientId = clientId,
            ClientNameSnapshot = clientNameSnapshot,
            RetainerAgreementId = retainerAgreementId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = InvoiceStatus.Draft,
            IssuedAt = DateTime.UtcNow
        };
    }

    public void AddLineItem(string description, decimal amount)
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Line items can only be added to a Draft invoice.");

        _lineItems.Add(InvoiceLineItem.Create(Id, description, amount));
    }

    public void MarkAsSent()
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("Only a Draft invoice can be sent.");
        Status = InvoiceStatus.Sent;
    }

    public void MarkAsPaid()
    {
        // تسجيل الدفع مرتين مايطلعش Event مرتين (يعني إيميل "تم الدفع" مرتين)
        if (Status == InvoiceStatus.Paid)
            return;
        if (Status == InvoiceStatus.Void)
            throw new DomainException("Cannot mark a Void invoice as Paid.");
        // كان مسموح Draft → Paid، وده بيكسر دورة الحياة (FR-BI-5): فاتورة مبعتتش مينفعش تتدفع
        if (Status == InvoiceStatus.Draft)
            throw new DomainException("A Draft invoice must be sent before it can be paid.");

        Status = InvoiceStatus.Paid;
        PaidAt = DateTime.UtcNow;
        _domainEvents.Add(new InvoicePaidEvent(Id, WorkspaceId, ClientId, TotalAmount));
    }
}
