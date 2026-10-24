using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.TimeTracking.Queries.GetPendingTimeEntries;

public record GetPendingTimeEntriesQuery(int Page = 1, int PageSize = 50)
    : IRequest<IReadOnlyList<PendingTimeEntryDto>>;

// DTO مش الـ Entity: HourlyCostAtTimeOfEntry مش بنرجّعه لأنه بيانات مالية حساسة (تكلفة الموظف)
// والـ PM مش المفروض يشوف تكلفة زمايله
public record PendingTimeEntryDto(
    Guid Id, Guid TaskItemId, Guid UserId, decimal DurationHours,
    bool IsBillable, string? Note, DateTime CreatedAt);

public class GetPendingTimeEntriesQueryHandler
    : IRequestHandler<GetPendingTimeEntriesQuery, IReadOnlyList<PendingTimeEntryDto>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetPendingTimeEntriesQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<PendingTimeEntryDto>> Handle(
        GetPendingTimeEntriesQuery request, CancellationToken ct)
    {
        // حدود الـ Pagination: من غيرها، Workspace كبير يرجّع آلاف الصفوف في طلب واحد
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(request.Page, 1);

        return await _dbContext.TimeEntries
            .AsNoTracking()
            .Where(t => !t.IsApproved)
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id) // ترتيب ثابت، وإلا الصفحات بتتكرر/بتتخطى
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new PendingTimeEntryDto(
                t.Id, t.TaskItemId, t.UserId, t.DurationHours, t.IsBillable, t.Note, t.CreatedAt))
            .ToListAsync(ct);
    }
}
