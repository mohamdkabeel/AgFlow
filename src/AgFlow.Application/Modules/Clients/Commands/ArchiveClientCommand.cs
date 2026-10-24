using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Common;

namespace AgFlow.Application.Modules.Clients.Commands.ArchiveClient;

// FR-CM-7: أرشفة بدل حذف (US-1.5): الفواتير والساعات القديمة لازم تفضل سليمة
public record ArchiveClientCommand(Guid ClientId) : IRequest;
public record ReactivateClientCommand(Guid ClientId) : IRequest;

public class ArchiveClientCommandHandler : IRequestHandler<ArchiveClientCommand>
{
    private readonly IApplicationDbContext _dbContext;
    public ArchiveClientCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(ArchiveClientCommand request, CancellationToken ct)
    {
        var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found.");

        // مانوقفش الـ Retainer ضمنياً: إيقافه قرار مالي بتاريخ نهاية، ولازم يتاخد صراحةً
        // (وإلا الأرشفة كانت هتوقف الفوترة بصمت من غير ما حد ياخد باله)
        var hasActiveRetainer = await _dbContext.RetainerAgreements
            .AnyAsync(r => r.ClientId == client.Id && r.IsActive, ct);
        if (hasActiveRetainer)
            throw new DomainException("Deactivate the client's active retainer before archiving the client.");

        client.Archive();
        await _dbContext.SaveChangesAsync(ct);
    }
}

public class ReactivateClientCommandHandler : IRequestHandler<ReactivateClientCommand>
{
    private readonly IApplicationDbContext _dbContext;
    public ReactivateClientCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(ReactivateClientCommand request, CancellationToken ct)
    {
        var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found.");

        client.Reactivate();
        await _dbContext.SaveChangesAsync(ct);
    }
}
