using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Projects.Commands.CreateTask;
using AgFlow.Application.Modules.Projects.Commands.UpdateTaskStatus;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/tasks")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;

    public TasksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Admin,ProjectManager")] // FR-PM-4: PM بينشئ Tasks
    public async Task<IActionResult> Create([FromBody] CreateTaskCommand command)
    {
        var taskId = await _mediator.Send(command);
        return CreatedAtAction(nameof(Create), new { id = taskId }, new { id = taskId });
    }

    // لاحظ: مفيش [Authorize(Roles=...)] هنا — أي Role ممكن يعدّل حالة Task،
    // لكن الـ Resource-Scoped check جوه الـ Handler هو اللي بيقرر فعلياً (FR-PM-6)
    [HttpPatch("{taskId}/status")]
    public async Task<IActionResult> UpdateStatus(Guid taskId, [FromBody] UpdateTaskStatusRequest request)
    {
        await _mediator.Send(new UpdateTaskStatusCommand(taskId, request.NewStatus));
        return NoContent();
    }
}

public record UpdateTaskStatusRequest(AgFlow.Domain.Modules.Projects.Entities.TaskItemStatus NewStatus);
