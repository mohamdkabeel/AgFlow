using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.Application.Modules.Clients.Queries.ListClients;

public record ListClientsQuery(ClientStatus? Status = ClientStatus.Active, string? Search = null,
    int Page = 1, int PageSize = 25) : IRequest<IReadOnlyList<ClientListItemDto>>;

// عمداً مفيش سعر الـ Retainer هنا: القائمة بيشوفها كل الأدوار الداخلية (حتى TeamMember)،
// والأسعار بيانات مالية لـ Owner/Admin/Finance بس (endpoint منفصل تحت)
public record ClientListItemDto(Guid Id, string CompanyName, ClientStatus Status,
    DateTime CreatedAt, bool HasActiveRetainer);

public class ListClientsQueryHandler : IRequestHandler<ListClientsQuery, IReadOnlyList<ClientListItemDto>>
{
    private readonly IApplicationDbContext _dbContext;
    public ListClientsQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<ClientListItemDto>> Handle(ListClientsQuery request, CancellationToken ct)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(request.Page, 1);

        var query = _dbContext.Clients.AsNoTracking().AsQueryable();

        if (request.Status is { } status)
            query = query.Where(c => c.Status == status);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.CompanyName.Contains(term));
        }

        return await query
            .OrderBy(c => c.CompanyName).ThenBy(c => c.Id) // ترتيب ثابت للـ Pagination
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClientListItemDto(
                c.Id, c.CompanyName, c.Status, c.CreatedAt,
                _dbContext.RetainerAgreements.Any(r => r.ClientId == c.Id && r.IsActive)))
            .ToListAsync(ct);
    }
}
