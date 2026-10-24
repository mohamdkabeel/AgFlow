using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Reports.Queries.Utilization;

public record GetUtilizationReportQuery(DateOnly From, DateOnly To) : IRequest<UtilizationReportResult>;

// عمداً مفيش تكلفة ساعة هنا: الـ PM بيشوف الأحمال، مش مرتبات الفريق (FR-RP-3)
public record MemberUtilizationRow(
    Guid UserId, string FullName, decimal LoggedHours, decimal BillableHours,
    decimal CapacityHours, decimal UtilizationPercent);

public record UtilizationReportResult(
    DateOnly From, DateOnly To, decimal WeeklyCapacityHoursPerMember, IReadOnlyList<MemberUtilizationRow> Members);

public class GetUtilizationReportQueryHandler
    : IRequestHandler<GetUtilizationReportQuery, UtilizationReportResult>
{
    public const int MaxRangeDays = 92; // ~ ربع سنة

    private readonly IApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public GetUtilizationReportQueryHandler(IApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<UtilizationReportResult> Handle(GetUtilizationReportQuery request, CancellationToken ct)
    {
        if (request.To < request.From)
            throw new ArgumentException("'To' must not be before 'From'.");
        var days = request.To.DayNumber - request.From.DayNumber + 1;
        if (days > MaxRangeDays)
            throw new ArgumentException($"Date range cannot exceed {MaxRangeDays} days.");

        // افتراض مؤقت موثّق: الطاقة = ساعات أسبوعية ثابتة لكل عضو (الافتراضي 40) × (الأيام ÷ 7).
        // مش بنفترض أيام عمل (الجمعة/السبت ولا الأحد؟) لأنها بتختلف من بلد لبلد. وطاقة كل عضو بشكل مستقل (إجازات، دوام جزئي)
        // محتاجة إعداد على العضو نفسه، وده قرار منتج لاحق
        var weekly = _configuration.GetValue<decimal?>("Reports:StandardWeeklyHours") ?? 40m;
        var capacity = Math.Round(weekly * days / 7m, 1);

        var logged = await _db.TimeEntries.AsNoTracking()
            .Where(t => t.WorkDate >= request.From && t.WorkDate <= request.To)
            .GroupBy(t => t.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Sum(x => x.DurationHours),
                Billable = g.Sum(x => x.IsBillable ? x.DurationHours : 0m)
            }).ToListAsync(ct);
        var loggedIds = logged.Select(l => l.UserId).ToList();

        // الأعضاء النشطين (حتى لو ماسجلوش حاجة: 0% معلومة مهمة للـ PM)، + أي حد سجّل في الفترة واتعطّل بعدها.
        // البداية من WorkspaceMembers (عليه Query Filter) وبعدين Join مع Users، زي ListMembers
        var members = await (
            from m in _db.WorkspaceMembers.AsNoTracking()
            where m.Status == MembershipStatus.Active || loggedIds.Contains(m.UserId)
            join u in _db.Users.AsNoTracking() on m.UserId equals u.Id
            select new { m.UserId, u.FullName }).ToListAsync(ct);

        var rows = members.Select(m =>
        {
            var l = logged.FirstOrDefault(x => x.UserId == m.UserId);
            var total = l?.Total ?? 0m;
            return new MemberUtilizationRow(
                m.UserId, m.FullName, total, l?.Billable ?? 0m, capacity,
                capacity > 0 ? Math.Round(total / capacity * 100m, 1) : 0m);
        })
        .OrderByDescending(r => r.UtilizationPercent).ThenBy(r => r.FullName)
        .ToList();

        return new UtilizationReportResult(request.From, request.To, weekly, rows);
    }
}
