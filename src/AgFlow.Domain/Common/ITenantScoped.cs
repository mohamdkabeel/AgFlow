namespace AgFlow.Domain.Common;

// أي Entity بتنفذ الـ Interface الفاضي ده بتاخد Global Query Filter تلقائياً
// (Client, Project, TimeEntry, Invoice... لكن مش Plan أو PlanFeature)
public interface ITenantScoped
{
    Guid WorkspaceId { get; }
}
