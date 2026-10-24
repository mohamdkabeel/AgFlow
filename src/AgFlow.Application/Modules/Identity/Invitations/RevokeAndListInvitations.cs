using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Invitations;

public record RevokeInvitationCommand(Guid InvitationId) : IRequest;

public class RevokeInvitationCommandHandler : IRequestHandler<RevokeInvitationCommand>
{
    private readonly IApplicationDbContext _dbContext;
    public RevokeInvitationCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(RevokeInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _dbContext.Invitations
            .FirstOrDefaultAsync(i => i.Id == request.InvitationId, ct)
            ?? throw new KeyNotFoundException("Invitation not found.");

        invitation.Revoke();
        await _dbContext.SaveChangesAsync(ct);
    }
}

public record ListInvitationsQuery(InvitationStatus? Status = InvitationStatus.Pending)
    : IRequest<IReadOnlyList<InvitationDto>>;

// مفيش TokenHash في الـ DTO
public record InvitationDto(Guid Id, string Email, WorkspaceRole Role, InvitationStatus Status,
    DateTime CreatedAt, DateTime ExpiresAt, bool IsExpired);

public class ListInvitationsQueryHandler : IRequestHandler<ListInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    private readonly IApplicationDbContext _dbContext;
    public ListInvitationsQueryHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<InvitationDto>> Handle(ListInvitationsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = _dbContext.Invitations.AsNoTracking().AsQueryable();
        if (request.Status is { } status)
            query = query.Where(i => i.Status == status);

        return await query
            .OrderByDescending(i => i.CreatedAt).ThenBy(i => i.Id)
            .Take(200)
            .Select(i => new InvitationDto(i.Id, i.Email, i.Role, i.Status, i.CreatedAt, i.ExpiresAt,
                i.Status == InvitationStatus.Pending && i.ExpiresAt <= now))
            .ToListAsync(ct);
    }
}
