using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Projects.Board;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.API.Controllers;

// Controllers منفصلة عمداً: عشان ما نلمسش ProjectsController/TasksController الموجودين عندك (فيهم تعديلاتك).
// [Tags] بيجمعهم في Swagger تحت نفس العنوان

[ApiController]
[Route("api/v{version:apiVersion}/projects")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
[Tags("Projects")]
public class ProjectBoardController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProjectBoardController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListProjectsQuery query) => Ok(await _mediator.Send(query));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) => Ok(await _mediator.Send(new GetProjectByIdQuery(id)));

    // FR-PM-3
    [HttpPost("{id:guid}/task-lists")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> CreateTaskList(Guid id, [FromBody] CreateTaskListRequest request)
    {
        var listId = await _mediator.Send(new CreateTaskListCommand(id, request.Name));
        return Created($"api/v1/projects/{id}", new { id = listId });
    }

    // FR-PM-8
    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> Complete(Guid id)
    {
        await _mediator.Send(new ChangeProjectStatusCommand(id, ProjectStatus.Completed));
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> Archive(Guid id)
    {
        await _mediator.Send(new ChangeProjectStatusCommand(id, ProjectStatus.Archived));
        return NoContent();
    }
}

public record CreateTaskListRequest(string Name);

[ApiController]
[Route("api/v{version:apiVersion}/tasks")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
[Tags("Tasks")]
public class TaskQueriesController : ControllerBase
{
    private readonly IMediator _mediator;
    public TaskQueriesController(IMediator mediator) => _mediator = mediator;

    // ?assignedToMe=true بيرجّع "مهامي" بس
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListTasksQuery query) => Ok(await _mediator.Send(query));
}
