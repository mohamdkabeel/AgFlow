using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Members;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/members")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
public class MembersController : ControllerBase
{
    private readonly IMediator _mediator;
    public MembersController(IMediator mediator) => _mediator = mediator;

    // الأسماء بتتشاف لتوزيع الشغل. التكلفة جوه الـ Handler بتتخفي عن غير المصرّح لهم
    [HttpGet]
    [Authorize(Roles = "Owner,Admin,ProjectManager,Finance")]
    public async Task<IActionResult> List() => Ok(await _mediator.Send(new ListMembersQuery()));

    public record ChangeRoleRequest(WorkspaceRole Role);
    public record UpdateCostRequest(decimal HourlyCost);

    [HttpPut("{id:guid}/role")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> ChangeRole(Guid id, [FromBody] ChangeRoleRequest request)
    {
        await _mediator.Send(new ChangeMemberRoleCommand(id, request.Role));
        return NoContent();
    }

    [HttpPut("{id:guid}/hourly-cost")]
    [Authorize(Roles = "Owner,Admin,Finance")]
    public async Task<IActionResult> UpdateCost(Guid id, [FromBody] UpdateCostRequest request)
    {
        await _mediator.Send(new UpdateMemberCostCommand(id, request.HourlyCost));
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _mediator.Send(new DeactivateMemberCommand(id));
        return NoContent();
    }
}
