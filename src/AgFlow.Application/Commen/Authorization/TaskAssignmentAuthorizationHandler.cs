using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Projects.Entities; // TaskItem — هنبنيها فعلياً في Phase 9

namespace AgFlow.Application.Common.Authorization;

// AuthorizationHandler<Requirement, Resource> — الجزء التاني (TaskItem) هو نوع
// الـ "مورد" اللي بنتحقق عليه، مش بس نوع المستخدم
public class TaskAssignmentAuthorizationHandler
    : AuthorizationHandler<TaskAssignmentRequirement, TaskItem>
{
    private readonly IApplicationDbContext _dbContext;

    public TaskAssignmentAuthorizationHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TaskAssignmentRequirement requirement,
        TaskItem resource)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);

        // القاعدة: Admin/Owner/PM يقدروا يعدّلوا أي Task في الـ Workspace بتاعهم
        // (الـ Workspace isolation نفسها بتتفرض في مكان تاني — Step 5 الجاية)
        if (role is "Owner" or "Admin" or "ProjectManager")
        {
            context.Succeed(requirement);
            return;
        }

        // لو Team Member عادي، لازم نتحقق: هو فعلاً متكلف بالـ Task دي؟
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return; // مفيش Fail صريح — عدم استدعاء Succeed معناها Fail تلقائياً
        }

        var isAssigned = await _dbContext.TaskAssignments
            .AnyAsync(a => a.TaskItemId == resource.Id && a.UserId == userId);

        if (isAssigned)
        {
            context.Succeed(requirement);
        }
        // لو مش متكلف بيها، معملناش Succeed → النتيجة تلقائياً Fail
    }
}
