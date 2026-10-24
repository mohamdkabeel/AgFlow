using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Billing.Entities;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.Application.Modules.Portal;

// ============================================================================
// FR-RP-6: DTOs الـ Portal "Allow-list": بنقول إيه اللي يظهر، مش بنشيل اللي ممنوع.
// مفيش هنا: HourlyCostAtTimeOfEntry، TimeEntries، Profitability، أسعار الـ Retainer،
// ولا أسماء/تفاصيل Tasks الداخلية. لو حد ضاف Field للـ Entity مستقبلاً، مش هيتسرّب لوحده.
// ============================================================================

// ---------- المشاريع ----------
public record PortalProjectDto(
    Guid Id, string Name, string Type, string Status,
    int TotalTasks, int DoneTasks, int InReviewTasks);

public record GetPortalProjectsQuery : IRequest<List<PortalProjectDto>>;

public class GetPortalProjectsQueryHandler : IRequestHandler<GetPortalProjectsQuery, List<PortalProjectDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentPortalUser _portalUser;
    public GetPortalProjectsQueryHandler(IApplicationDbContext db, ICurrentPortalUser portalUser)
    { _db = db; _portalUser = portalUser; }

    public async Task<List<PortalProjectDto>> Handle(GetPortalProjectsQuery request, CancellationToken ct)
    {
        var me = await PortalGuard.RequireAsync(_portalUser, _db, ct);

        // الفلتر بـ ClientId من الـ Token (مش من الـ Request) + الـ Tenant Filter التلقائي = طبقتين عزل
        var projects = await _db.Projects.AsNoTracking()
            .Where(p => p.ClientId == me.ClientId && p.Status != ProjectStatus.Archived)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Type, p.Status })
            .ToListAsync(ct);

        var ids = projects.Select(p => p.Id).ToList();

        // عدّادات بس (من غير عناوين المهام). Group في الـ DB بدل ما نجيب المهام كلها
        var counts = await _db.TaskItems.AsNoTracking()
            .Join(_db.TaskLists.Where(l => ids.Contains(l.ProjectId)),
                  t => t.TaskListId, l => l.Id,
                  (t, l) => new { l.ProjectId, t.Status })
            .GroupBy(x => new { x.ProjectId, x.Status })
            .Select(g => new { g.Key.ProjectId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        return projects.Select(p =>
        {
            var mine = counts.Where(c => c.ProjectId == p.Id).ToList();
            return new PortalProjectDto(
                p.Id, p.Name, p.Type.ToString(), p.Status.ToString(),
                mine.Sum(c => c.Count),
                mine.Where(c => c.Status == TaskItemStatus.Done).Sum(c => c.Count),
                mine.Where(c => c.Status == TaskItemStatus.InReview).Sum(c => c.Count));
        }).ToList();
    }
}

// ---------- الفواتير ----------
public record PortalInvoiceSummaryDto(
    Guid Id, string Status, DateTime IssuedAt, DateTime? PaidAt,
    DateTime? PeriodStart, DateTime? PeriodEnd, decimal TotalAmount);

public record PortalInvoiceLineDto(string Description, decimal Amount);

public record PortalInvoiceDetailDto(
    Guid Id, string Status, DateTime IssuedAt, DateTime? PaidAt,
    DateTime? PeriodStart, DateTime? PeriodEnd, decimal TotalAmount,
    List<PortalInvoiceLineDto> Lines);

internal static class PortalInvoiceRules
{
    // Draft = لسه الوكالة بتراجعها (ممكن تتغير/تتحذف)، Void = ملغية. العميل مايشوفهمش أبداً
    public static readonly InvoiceStatus[] Hidden = { InvoiceStatus.Draft, InvoiceStatus.Void };
}

public record GetPortalInvoicesQuery : IRequest<List<PortalInvoiceSummaryDto>>;

public class GetPortalInvoicesQueryHandler : IRequestHandler<GetPortalInvoicesQuery, List<PortalInvoiceSummaryDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentPortalUser _portalUser;
    public GetPortalInvoicesQueryHandler(IApplicationDbContext db, ICurrentPortalUser portalUser)
    { _db = db; _portalUser = portalUser; }

    public async Task<List<PortalInvoiceSummaryDto>> Handle(GetPortalInvoicesQuery request, CancellationToken ct)
    {
        var me = await PortalGuard.RequireAsync(_portalUser, _db, ct);
        var hidden = PortalInvoiceRules.Hidden;

        return await _db.Invoices.AsNoTracking()
            .Where(i => i.ClientId == me.ClientId && !hidden.Contains(i.Status))
            .OrderByDescending(i => i.IssuedAt)
            .Take(200)
            .Select(i => new PortalInvoiceSummaryDto(
                i.Id, i.Status.ToString(), i.IssuedAt, i.PaidAt, i.PeriodStart, i.PeriodEnd,
                i.LineItems.Sum(li => li.Amount)))
            .ToListAsync(ct);
    }
}

public record GetPortalInvoiceQuery(Guid InvoiceId) : IRequest<PortalInvoiceDetailDto>;

public class GetPortalInvoiceQueryHandler : IRequestHandler<GetPortalInvoiceQuery, PortalInvoiceDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentPortalUser _portalUser;
    public GetPortalInvoiceQueryHandler(IApplicationDbContext db, ICurrentPortalUser portalUser)
    { _db = db; _portalUser = portalUser; }

    public async Task<PortalInvoiceDetailDto> Handle(GetPortalInvoiceQuery request, CancellationToken ct)
    {
        var me = await PortalGuard.RequireAsync(_portalUser, _db, ct);
        var hidden = PortalInvoiceRules.Hidden;

        // الـ ClientId جوه الـ Where نفسه: فاتورة عميل تاني (حتى في نفس الوكالة) = 404 مش 403،
        // عشان ماندّيش تلميح إن الـ Id ده موجود (IDOR)
        var invoice = await _db.Invoices.AsNoTracking()
            .Where(i => i.Id == request.InvoiceId && i.ClientId == me.ClientId && !hidden.Contains(i.Status))
            .Select(i => new
            {
                i.Id, i.Status, i.IssuedAt, i.PaidAt, i.PeriodStart, i.PeriodEnd,
                Lines = i.LineItems.Select(li => new PortalInvoiceLineDto(li.Description, li.Amount)).ToList()
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Invoice not found.");

        return new PortalInvoiceDetailDto(
            invoice.Id, invoice.Status.ToString(), invoice.IssuedAt, invoice.PaidAt,
            invoice.PeriodStart, invoice.PeriodEnd, invoice.Lines.Sum(l => l.Amount), invoice.Lines);
    }
}
