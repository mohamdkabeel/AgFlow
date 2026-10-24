using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.Clients.Queries.GetClientRetainers;

public record GetClientRetainersQuery(Guid ClientId) : IRequest<IReadOnlyList<RetainerDto>>;

public record RetainerDto(Guid Id, int MonthlyHours, decimal MonthlyPrice, bool RolloverEnabled,
    bool IsActive, DateOnly StartDate, DateOnly? EndDate);

public class GetClientRetainersQueryHandler
    : IRequestHandler<GetClientRetainersQuery, IReadOnlyList<RetainerDto>>
{
    private readonly IApplicationDbContext _dbContext;
    public GetClientRetainersQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<RetainerDto>> Handle(GetClientRetainersQuery request, CancellationToken ct)
    {
        // التاريخ كله (النشط + اللي اتوقفت) مرتب من الأحدث: ده سجل تغيّر الأسعار
        return await _dbContext.RetainerAgreements
            .AsNoTracking()
            .Where(r => r.ClientId == request.ClientId)
            .OrderByDescending(r => r.StartDate)
            .Select(r => new RetainerDto(r.Id, r.MonthlyHours, r.MonthlyPrice, r.RolloverEnabled,
                r.IsActive, r.StartDate, r.EndDate))
            .ToListAsync(ct);
    }
}
