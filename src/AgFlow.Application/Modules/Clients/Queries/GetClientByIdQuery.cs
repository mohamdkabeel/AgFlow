using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.Clients.Queries.GetClientById;

public record GetClientByIdQuery(Guid ClientId) : IRequest<ClientDto?>;

// DTO بسيط بس بالحقول اللي فعلاً محتاجينها في الرد —
// مش الـ Entity الكاملة بكل الـ methods والمنطق الداخلي بتاعها
public record ClientDto(Guid Id, string CompanyName, string Status, int ContactsCount);

public class GetClientByIdQueryHandler : IRequestHandler<GetClientByIdQuery, ClientDto?>
{
    private readonly IApplicationDbContext _dbContext;

    public GetClientByIdQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClientDto?> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
    {
        // AsNoTracking: قول لـ EF Core "متتبعش تغييرات على البيانات دي،
        // إحنا هنعرضها بس ومش هنعدلها" — تحسين أداء مباشر
        //
        // Select(...): بنجيب بس الحقول اللي محتاجينها (Projection)،
        // مش نجيب الـ Entity كاملة ونرمي الباقي بعدين
        //
        // لاحظ: WorkspaceId Filter اتفرض هنا تلقائياً من غير ما نكتبه —
        // ده الـ Global Query Filter شغال هنا برضه، مش بس في الـ Commands
        var client = await _dbContext.Clients
            .AsNoTracking()
            .Where(c => c.Id == request.ClientId)
            .Select(c => new ClientDto(
                c.Id,
                c.CompanyName,
                c.Status.ToString(),
                c.Contacts.Count))
            .FirstOrDefaultAsync(cancellationToken);

        return client;
    }
}
