using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Billing.Entities;

public enum OverageDecision
{
    Pending,
    Approved,
    Rejected
}

// FR-BI-3: سجل قرار بشري صريح — مفيش Overage يتحول لفلوس من غيره
public class OverageApproval : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid InvoiceId { get; private set; } // الفاتورة (يعني الفترة) اللي الـ Overage اتحسب عليها
    public decimal OverageHours { get; private set; }
    public decimal OverageAmount { get; private set; }
    public OverageDecision Decision { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private OverageApproval() { }

    public static OverageApproval CreatePending(
        Guid workspaceId, Guid clientId, Guid invoiceId, decimal overageHours, decimal overageAmount)
    {
        return new OverageApproval
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClientId = clientId,
            InvoiceId = invoiceId,
            OverageHours = overageHours,
            OverageAmount = overageAmount,
            Decision = OverageDecision.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(Guid decidedByUserId)
    {
        if (Decision != OverageDecision.Pending)
            throw new DomainException("This overage has already been decided.");
        Decision = OverageDecision.Approved;
        DecidedByUserId = decidedByUserId;
    }

    public void Reject(Guid decidedByUserId)
    {
        if (Decision != OverageDecision.Pending)
            throw new DomainException("This overage has already been decided.");
        Decision = OverageDecision.Rejected;
        DecidedByUserId = decidedByUserId;
    }
}
