using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.Portal;

public record GetPortalMeQuery : IRequest<PortalMeDto>;

// DTO صغير عمداً: اسم الشخص والشركة بس. مفيش أي حاجة داخلية (Ids الداخلية، أسعار، تكلفة)
public record PortalMeDto(string ContactName, string Email, string CompanyName, string AgencyName);

public class GetPortalMeQueryHandler : IRequestHandler<GetPortalMeQuery, PortalMeDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentPortalUser _portalUser;

    public GetPortalMeQueryHandler(IApplicationDbContext db, ICurrentPortalUser portalUser)
    {
        _db = db;
        _portalUser = portalUser;
    }

    public async Task<PortalMeDto> Handle(GetPortalMeQuery request, CancellationToken ct)
    {
        var me = await PortalGuard.RequireAsync(_portalUser, _db, ct);

        var row = await _db.Clients.AsNoTracking()
            .Where(c => c.Id == me.ClientId)
            .Select(c => new
            {
                c.CompanyName,
                Contact = c.Contacts.Where(x => x.Id == me.ContactId).Select(x => new { x.FullName, x.Email }).FirstOrDefault()
            }).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Client not found.");
        if (row.Contact is null) throw new UnauthorizedAccessException("Portal access is no longer available.");

        var agency = await _db.Workspaces.AsNoTracking()
            .Where(w => w.Id == me.WorkspaceId).Select(w => w.Name).FirstAsync(ct);

        return new PortalMeDto(row.Contact.FullName, row.Contact.Email, row.CompanyName, agency);
    }
}
