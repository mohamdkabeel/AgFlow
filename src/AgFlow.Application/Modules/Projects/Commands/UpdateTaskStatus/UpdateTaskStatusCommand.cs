using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using AgFlow.Application.Common.Authorization;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.Application.Modules.Projects.Commands.UpdateTaskStatus;

public record UpdateTaskStatusCommand(Guid TaskId, TaskItemStatus NewStatus) : IRequest;

public class UpdateTaskStatusCommandHandler : IRequestHandler<UpdateTaskStatusCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserPrincipalAccessor _principalAccessor;

    public UpdateTaskStatusCommandHandler(
        IApplicationDbContext dbContext,
        IAuthorizationService authorizationService,
        ICurrentUserPrincipalAccessor principalAccessor)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _principalAccessor = principalAccessor;
    }

    public async Task Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        // تصحيح: FirstOrDefaultAsync (مش FindAsync). الأخيرة بتدور في الـ Change Tracker الأول
        // وسلوكها مع الـ Global Query Filters مش مضمون، فمينفعش نبني عليها حماية الـ Tenant.
        // FirstOrDefaultAsync بيمر على الفلتر بالتأكيد.
        var task = await _dbContext.TaskItems
            .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException("Task not found.");

        // هنا بنستدعي الـ Handler اللي بنيناه في Phase 8, Step 4 فعلياً
        var authResult = await _authorizationService.AuthorizeAsync(
            _principalAccessor.User, task, new TaskAssignmentRequirement());

        if (!authResult.Succeeded)
            throw new UnauthorizedAccessException("You are not assigned to this task.");

        task.UpdateStatus(request.NewStatus);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
