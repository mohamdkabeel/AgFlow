using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.Application.Modules.Reports.Queries.Dashboard;

public record GetDashboardQuery : IRequest<DashboardResult>;

public record UpcomingDeadlineDto(Guid TaskId, string Title, DateTime DueDate, string ProjectName, string ClientName);

// US-3.4 / US-8.1: "25/40 ساعة مستخدمة". الساعات المعتمدة بس بتتحسب (FR-TT-6)، والمعلّقة منفصلة
public record RetainerHealthDto(
    Guid ClientId, string ClientName, int MonthlyHours,
    decimal UsedHours, decimal PendingApprovalHours, decimal UsagePercent, bool IsOverage);

public record DashboardResult(
    int ActiveClients, int ActiveProjects,
    decimal MonthlyRecurringRevenue, // مجموع أسعار الـ Retainers النشطة (FR-RP-1)
    int OverdueTasks,
    IReadOnlyList<UpcomingDeadlineDto> UpcomingDeadlines,
    IReadOnlyList<RetainerHealthDto> RetainerHealth);

public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardResult>
{
    private readonly IApplicationDbContext _db;
    public GetDashboardQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<DashboardResult> Handle(GetDashboardQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var startDay = DateOnly.FromDateTime(monthStart);
        var endDay = DateOnly.FromDateTime(monthStart.AddMonths(1));

        // مفيش WorkspaceId في أي استعلام هنا: كله من الـ Query Filter التلقائي
        // (ده الدليل العملي إن التصميم شغال: 7 استعلامات ومحدش فيهم ينفع ينسى الفلتر)
        var activeClients = await _db.Clients.CountAsync(c => c.Status == Domain.Modules.Clients.Entities.ClientStatus.Active, ct);
        var activeProjects = await _db.Projects.CountAsync(p => p.Status == ProjectStatus.Active, ct);
        var mrr = await _db.RetainerAgreements.Where(r => r.IsActive).SumAsync(r => r.MonthlyPrice, ct);

        // المهام بتتحسب بس لو مشروعها نشط: مهمة متأخرة في مشروع اتأرشف مش "ضجيج" نبهه للـ Owner
        var overdue = await (
            from ti in _db.TaskItems
            where ti.Status != TaskItemStatus.Done && ti.DueDate != null && ti.DueDate < now
            join tl in _db.TaskLists on ti.TaskListId equals tl.Id
            join p in _db.Projects on tl.ProjectId equals p.Id
            where p.Status == ProjectStatus.Active
            select ti.Id).CountAsync(ct);

        var upcoming = await (
            from ti in _db.TaskItems.AsNoTracking()
            where ti.Status != TaskItemStatus.Done && ti.DueDate != null
                && ti.DueDate >= now && ti.DueDate < now.AddDays(7)
            join tl in _db.TaskLists on ti.TaskListId equals tl.Id
            join p in _db.Projects on tl.ProjectId equals p.Id
            where p.Status == ProjectStatus.Active
            join c in _db.Clients on p.ClientId equals c.Id
            orderby ti.DueDate, ti.Id
            select new UpcomingDeadlineDto(ti.Id, ti.Title, (DateTime)ti.DueDate!, p.Name, c.CompanyName))
            .Take(10).ToListAsync(ct);

        // === صحة الـ Retainers: الشهر الحالي ===
        var usage = await (
            from te in _db.TimeEntries.AsNoTracking()
            where te.WorkDate >= startDay && te.WorkDate < endDay && te.IsBillable
            join ti in _db.TaskItems on te.TaskItemId equals ti.Id
            join tl in _db.TaskLists on ti.TaskListId equals tl.Id
            join p in _db.Projects on tl.ProjectId equals p.Id
            where p.RetainerAgreementId != null
            group te by p.RetainerAgreementId into g
            select new
            {
                RetainerId = g.Key,
                Approved = g.Sum(x => x.IsApproved ? x.DurationHours : 0m),
                Pending = g.Sum(x => !x.IsApproved ? x.DurationHours : 0m)
            }).ToListAsync(ct);

        var retainers = await (
            from r in _db.RetainerAgreements.AsNoTracking()
            where r.IsActive
            join c in _db.Clients on r.ClientId equals c.Id
            select new { RetainerId = r.Id, ClientId = c.Id, c.CompanyName, r.MonthlyHours }).ToListAsync(ct);

        var health = retainers.Select(r =>
        {
            var u = usage.FirstOrDefault(x => x.RetainerId == r.RetainerId);
            var used = u?.Approved ?? 0m;
            return new RetainerHealthDto(
                r.ClientId, r.CompanyName, r.MonthlyHours, used, u?.Pending ?? 0m,
                Math.Round(used / r.MonthlyHours * 100m, 1), used > r.MonthlyHours);
        })
        .OrderByDescending(h => h.UsagePercent).ThenBy(h => h.ClientName) // الأقرب للتجاوز الأول
        .ToList();

        return new DashboardResult(activeClients, activeProjects, mrr, overdue, upcoming, health);
    }
}
