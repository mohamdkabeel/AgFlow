using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Projects.Commands.CreateProject;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/projects")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // FR-PM-1: Owner/Admin/PM
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> Create([FromBody] CreateProjectCommand command)
    {
        var projectId = await _mediator.Send(command);
        return CreatedAtAction(nameof(Create), new { id = projectId }, new { id = projectId });
    }
}
