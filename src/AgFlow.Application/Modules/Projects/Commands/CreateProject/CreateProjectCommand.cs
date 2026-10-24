using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.Application.Modules.Projects.Commands.CreateProject;

public record CreateProjectCommand(
    Guid ClientId,
    string Name,
    Guid? RetainerAgreementId) : IRequest<Guid>;

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateProjectCommandHandler(
        IApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = _currentUserService.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        // === Fix أمني (IDOR): نتأكد إن الـ Client والـ Retainer فعلاً تابعين لنفس الـ Workspace ===
        // الـ Global Filter بيطبق على الاستعلام ده، فلو الـ ID بتاع Workspace تاني هيرجع false
        var clientExists = await _dbContext.Clients
            .AnyAsync(c => c.Id == request.ClientId, cancellationToken);
        if (!clientExists)
            throw new KeyNotFoundException("Client not found.");

        if (request.RetainerAgreementId is Guid rid)
        {
            var retainerValid = await _dbContext.RetainerAgreements
                .AnyAsync(r => r.Id == rid && r.ClientId == request.ClientId, cancellationToken);
            if (!retainerValid)
                throw new KeyNotFoundException("Retainer agreement not found for this client.");
        }

        // القرار (Retainer ولا Fixed-Scope) بيتحدد من وجود RetainerAgreementId من عدمه
        var project = request.RetainerAgreementId is Guid retainerId
            ? Project.CreateRetainerProject(workspaceId, request.ClientId, retainerId, request.Name)
            : Project.CreateFixedScopeProject(workspaceId, request.ClientId, request.Name);

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return project.Id;
    }
}
