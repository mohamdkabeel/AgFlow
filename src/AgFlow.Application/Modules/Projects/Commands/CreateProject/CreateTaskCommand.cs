using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.Application.Modules.Projects.Commands.CreateTask;

public record CreateTaskCommand(
    Guid TaskListId,
    string Title,
    string? Description,
    DateTime? DueDate,
    List<Guid> AssigneeUserIds) : IRequest<Guid>;

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateTaskCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Guid> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = _currentUserService.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        // === Fix أمني (IDOR) 1: الـ TaskList لازم تكون تابعة لنفس الـ Workspace ===
        // (الاستعلام ده بيمر على الـ Global Filter، فأي ID من Workspace تاني بيرجع false)
        var taskListExists = await _dbContext.TaskLists
            .AnyAsync(t => t.Id == request.TaskListId, cancellationToken);
        if (!taskListExists)
            throw new KeyNotFoundException("Task list not found.");

        // === Fix أمني (IDOR) 2: كل Assignee لازم يكون عضو Active في نفس الـ Workspace ===
        // بنعد الـ UserIds المميزة (Distinct) عشان لو فيه صفوف عضوية مكررة الرقم ميبوظش
        var assigneeIds = request.AssigneeUserIds.Distinct().ToList();
        if (assigneeIds.Count > 0)
        {
            var validCount = await _dbContext.WorkspaceMembers
                .Where(m => assigneeIds.Contains(m.UserId) && m.Status == MembershipStatus.Active)
                .Select(m => m.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            // رسالة واحدة لحالة "مش موجود" و"موجود في Workspace تاني" — عشان ميبقاش فيه تسريب معلومات
            if (validCount != assigneeIds.Count)
                throw new KeyNotFoundException("One or more assignees were not found in this workspace.");
        }

        var task = TaskItem.Create(
            workspaceId, request.TaskListId, request.Title, request.Description, request.DueDate);

        // FR-PM-4: ممكن أكتر من شخص من نفس اللحظة — بنستفيد من AssignTo()
        // اللي بيحمي من التكرار داخل الـ Entity نفسها (Phase 9, Step 3)
        foreach (var userId in assigneeIds)
        {
            task.AssignTo(userId);
        }

        _dbContext.TaskItems.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return task.Id;
    }
}
