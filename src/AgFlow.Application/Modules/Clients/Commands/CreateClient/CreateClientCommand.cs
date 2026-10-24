using MediatR;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.Application.Modules.Clients.Commands.CreateClient;

// IRequest<Guid> يعني: الأمر ده هيرجع Guid (Id الـ Client الجديد) بعد التنفيذ
public record CreateClientCommand(string CompanyName) : IRequest<Guid>;

public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateClientCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        // لاحظ: مفيش أي مكان هنا بناخد فيه WorkspaceId من request نفسه —
        // بييجي بس من ICurrentUserService (اللي مصدره JWT)، زي ما اتفقنا في Phase 5, Step 3
        var workspaceId = _currentUserService.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        var client = Client.Create(workspaceId, request.CompanyName);

        _dbContext.Clients.Add(client);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return client.Id;
    }
}
