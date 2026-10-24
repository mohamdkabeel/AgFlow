using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Clients.Commands.DeactivateRetainer;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/retainers")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(Roles = "Owner,Admin")]
public class RetainersController : ControllerBase
{
    private readonly IMediator _mediator;
    public RetainersController(IMediator mediator) => _mediator = mediator;

    public record DeactivateRequest(DateOnly? EndDate);

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, [FromBody] DeactivateRequest? request)
    {
        await _mediator.Send(new DeactivateRetainerCommand(id, request?.EndDate));
        return NoContent();
    }
}
