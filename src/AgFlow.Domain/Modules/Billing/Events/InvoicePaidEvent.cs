using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Billing.Events;

// record لأن الـ Event ثابت (Immutable) — بيوصف "حاجة حصلت"، مش حاجة ممكن تتغير
public record InvoicePaidEvent(Guid InvoiceId, Guid WorkspaceId, Guid ClientId, decimal Amount) : IDomainEvent;
