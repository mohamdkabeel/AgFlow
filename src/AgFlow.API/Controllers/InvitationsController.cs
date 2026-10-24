using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AgFlow.Application.Modules.Invitations;
using AgFlow.Application.Modules.Invitations.Commands.AcceptInvitation;
using AgFlow.Application.Modules.Invitations.Commands.InviteMember;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/invitations")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(Roles = "Owner,Admin")]
public class InvitationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public InvitationsController(IMediator mediator) => _mediator = mediator;

    [HttpPost]
    public async Task<IActionResult> Invite([FromBody] InviteMemberCommand command)
        => Ok(await _mediator.Send(command));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] InvitationStatus? status = InvitationStatus.Pending)
        => Ok(await _mediator.Send(new ListInvitationsQuery(status)));

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id)
    {
        await _mediator.Send(new RevokeInvitationCommand(id));
        return NoContent();
    }

    // Anonymous عمداً (صاحب الدعوة لسه معندوش حساب/Token). AllowAnonymous على الـ Action بيتغلب على
    // الـ Authorize بتاع الـ Class. وعليه نفس Rate Limit بتاع الـ Login ضد تخمين الـ Tokens
    [HttpPost("accept")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationCommand command)
        => Ok(await _mediator.Send(command));
}
