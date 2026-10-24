using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Billing.Entities;

namespace AgFlow.Application.Modules.Billing.Queries.GetClientProfitability;

// ======================= الحساب المشترك =======================
// قواعد الحساب (مكان واحد، فالتقرير الشهري وتقرير العميل الواحد مايختلفوش في الأرقام أبداً):
//  - Revenue: فواتير مش Void، بتتنسب لـ "فترة الفاتورة" (PeriodStart) مش تاريخ الإصدار.
//    فاتورة سبتمبر الصادرة في أكتوبر بتتحسب على سبتمبر، جنب تكلفة ساعات سبتمبر. الفواتير اليدوية (من غير فترة) بتاريخ الإصدار.
//    الـ Draft محسوب (هو المبلغ المتفق عليه)، ومنفصل في DraftRevenue عشان Finance يعرف إنه لسه ماتبعتش.
//  - Cost: كل الساعات الفعلية (حتى غير المفوترة وغير المعتمدة): الموظف اتدفعله عنها فعلاً.
//    بـ HourlyCostAtTimeOfEntry المحفوظ (FR-TT-3)، فتغيير تكلفة موظف مابيغيّرش شهر فات.
//  - الوقت بـ WorkDate (يوم الشغل الفعلي)
internal static class ProfitabilityCalculator
{
    public static async Task<List<ClientProfitabilityRow>> ComputeAsync(
        IApplicationDbContext db, DateTime start, int monthCount, Guid? clientId, CancellationToken ct)
    {
        var end = start.AddMonths(monthCount);
        var startDay = DateOnly.FromDateTime(start);
        var endDay = DateOnly.FromDateTime(end);

        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Void
                && (i.PeriodStart ?? i.IssuedAt) >= start && (i.PeriodStart ?? i.IssuedAt) < end
                && (clientId == null || i.ClientId == clientId))
            .Select(i => new { i.ClientId, i.Status, Amount = i.LineItems.Sum(li => li.Amount) })
            .ToListAsync(ct);

        // Project → TaskList → TaskItem → TimeEntry في استعلام واحد، والتجميع في الـ SQL
        // (النسخة القديمة كانت بتسحب 3 قوايم Ids في الذاكرة وبتبعتها تاني للـ DB)
        var time = await (
            from te in db.TimeEntries.AsNoTracking()
            where te.WorkDate >= startDay && te.WorkDate < endDay
            join ti in db.TaskItems on te.TaskItemId equals ti.Id
            join tl in db.TaskLists on ti.TaskListId equals tl.Id
            join p in db.Projects on tl.ProjectId equals p.Id
            where clientId == null || p.ClientId == clientId
            group te by p.ClientId into g
            select new
            {
                ClientId = g.Key,
                Hours = g.Sum(x => x.DurationHours),
                Cost = g.Sum(x => x.DurationHours * x.HourlyCostAtTimeOfEntry),
                BillableApprovedHours = g.Sum(x => x.IsBillable && x.IsApproved ? x.DurationHours : 0m)
            }).ToListAsync(ct);

        var clientIds = invoices.Select(i => i.ClientId).Union(time.Select(t => t.ClientId)).ToList();
        // الأرشيف داخل (عميل اتأرشف النهاردة كان ليه شغل الشهر اللي فات)
        var names = await db.Clients.AsNoTracking()
            .Where(c => clientIds.Contains(c.Id))
            .Select(c => new { c.Id, c.CompanyName })
            .ToDictionaryAsync(c => c.Id, c => c.CompanyName, ct);

        return clientIds.Select(id =>
        {
            var inv = invoices.Where(i => i.ClientId == id).ToList();
            var t = time.FirstOrDefault(x => x.ClientId == id);
            var revenue = inv.Sum(i => i.Amount);
            var draft = inv.Where(i => i.Status == InvoiceStatus.Draft).Sum(i => i.Amount);
            var cost = t?.Cost ?? 0m;
            var profit = revenue - cost;
            return new ClientProfitabilityRow(
                id, names.GetValueOrDefault(id, "(unknown)"), revenue, draft, cost, profit,
                revenue > 0 ? Math.Round(profit / revenue * 100m, 1) : null,
                t?.Hours ?? 0m, t?.BillableApprovedHours ?? 0m);
        }).ToList();
    }
}

public record ClientProfitabilityRow(
    Guid ClientId, string ClientName, decimal Revenue, decimal DraftRevenue, decimal Cost, decimal Profit,
    decimal? MarginPercent, decimal TotalHours, decimal BillableApprovedHours);

// ======================= عميل واحد (الـ Endpoint القديم) =======================
public record GetClientProfitabilityQuery(Guid ClientId, int Year, int Month)
    : IRequest<ClientProfitabilityResult>;

public record ClientProfitabilityResult(decimal Revenue, decimal Cost, decimal Profit, decimal ActualHours);

public class GetClientProfitabilityQueryHandler
    : IRequestHandler<GetClientProfitabilityQuery, ClientProfitabilityResult>
{
    private readonly IApplicationDbContext _dbContext;
    public GetClientProfitabilityQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ClientProfitabilityResult> Handle(
        GetClientProfitabilityQuery request, CancellationToken ct)
    {
        // الـ Query Filter بيضمن إن العميل من نفس الـ Workspace. عميل مش موجود/تابع لـ Workspace تاني = 404
        if (!await _dbContext.Clients.AnyAsync(c => c.Id == request.ClientId, ct))
            throw new KeyNotFoundException("Client not found.");

        var rows = await ProfitabilityCalculator.ComputeAsync(
            _dbContext, new DateTime(request.Year, request.Month, 1), 1, request.ClientId, ct);
        var row = rows.FirstOrDefault();

        return row is null
            ? new ClientProfitabilityResult(0, 0, 0, 0)
            : new ClientProfitabilityResult(row.Revenue, row.Cost, row.Profit, row.TotalHours);
    }
}

// ======================= تقرير كل العملاء (FR-RP-2) =======================
// MonthCount: 1 = شهر، 3 = ربع سنة (الـ Controller بيترجم quarter → StartMonth + 3)
public record GetProfitabilityReportQuery(int Year, int StartMonth, int MonthCount)
    : IRequest<ProfitabilityReportResult>;

public record ProfitabilityReportResult(
    int Year, int StartMonth, int MonthCount,
    decimal TotalRevenue, decimal TotalCost, decimal TotalProfit,
    IReadOnlyList<ClientProfitabilityRow> Clients);

public class GetProfitabilityReportQueryHandler
    : IRequestHandler<GetProfitabilityReportQuery, ProfitabilityReportResult>
{
    private readonly IApplicationDbContext _dbContext;
    public GetProfitabilityReportQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<ProfitabilityReportResult> Handle(GetProfitabilityReportQuery request, CancellationToken ct)
    {
        var rows = await ProfitabilityCalculator.ComputeAsync(
            _dbContext, new DateTime(request.Year, request.StartMonth, 1), request.MonthCount, null, ct);

        // الأقل ربحاً الأول: الهدف من التقرير إنك تلاقي العملاء اللي بتخسر فيهم (قيمة المنتج الأساسية)
        var ordered = rows.OrderBy(r => r.Profit).ThenBy(r => r.ClientName).ToList();

        return new ProfitabilityReportResult(
            request.Year, request.StartMonth, request.MonthCount,
            ordered.Sum(r => r.Revenue), ordered.Sum(r => r.Cost), ordered.Sum(r => r.Profit), ordered);
    }
}
