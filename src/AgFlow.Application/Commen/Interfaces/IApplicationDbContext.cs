using Microsoft.EntityFrameworkCore;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Common.Interfaces;

// لاحظ: بنستخدم DbSet<T> هنا كنوع بيانات، لكن معندناش أي كود EF Core فعلي
// (زي UseSqlServer أو Migrations) — ده مجرد "عقد" بيقول
// "أي حاجة هتنفذ الـ Interface ده، لازم تقدر تديني الجداول دي"
public interface IApplicationDbContext
{
    DbSet<Workspace> Workspaces { get; }
    // تحذير: User مش ITenantScoped (مفيش Query Filter). ممنوع تتقري لوحدها أبداً،
    // لازم دايماً تتعمل Join مع WorkspaceMembers (اللي عليه Filter) عشان تفضل جوه الـ Workspace الحالي
    DbSet<User> Users { get; }
    DbSet<WorkspaceMember> WorkspaceMembers { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<AgFlow.Domain.Modules.Clients.Entities.Client> Clients { get; }
    DbSet<AgFlow.Domain.Modules.Clients.Entities.RetainerAgreement> RetainerAgreements { get; }
    DbSet<AgFlow.Domain.Modules.Clients.Entities.ClientPortalCredential> PortalCredentials { get; }
    DbSet<AgFlow.Domain.Modules.Projects.Entities.Project> Projects { get; }
    DbSet<AgFlow.Domain.Modules.Projects.Entities.TaskList> TaskLists { get; }
    DbSet<AgFlow.Domain.Modules.Projects.Entities.TaskItem> TaskItems { get; }
    DbSet<AgFlow.Domain.Modules.Projects.Entities.TaskAssignment> TaskAssignments { get; }
    DbSet<AgFlow.Domain.Modules.TimeTracking.Entities.TimeEntry> TimeEntries { get; }
    DbSet<AgFlow.Domain.Modules.Files.Entities.FileAsset> FileAssets { get; }
    DbSet<AgFlow.Domain.Modules.Billing.Entities.Invoice> Invoices { get; }
    DbSet<AgFlow.Domain.Modules.Billing.Entities.OverageApproval> OverageApprovals { get; }
    DbSet<AgFlow.Domain.Modules.Subscriptions.Entities.Subscription> Subscriptions { get; }
    DbSet<AgFlow.Domain.Modules.Notifications.Entities.Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // تجريد لمفهوم الـ Transaction، من غير ما نكشف نوع EF Core الفعلي (IDbContextTransaction)
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
