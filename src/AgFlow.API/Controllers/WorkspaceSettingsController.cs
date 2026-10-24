using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Identity.Settings;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/workspace/settings")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(Roles = "Owner,Admin")]
public class WorkspaceSettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    public WorkspaceSettingsController(IMediator mediator) => _mediator = mediator;

    public record TimeApprovalRequest(bool Required);

    [HttpPut("time-approval")]
    public async Task<IActionResult> SetTimeApproval([FromBody] TimeApprovalRequest request)
    {
        await _mediator.Send(new SetTimeApprovalCommand(request.Required));
        return NoContent();
    }
}
