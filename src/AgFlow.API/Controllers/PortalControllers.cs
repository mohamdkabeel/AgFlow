using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AgFlow.Application.Modules.Portal;

namespace AgFlow.API.Controllers;

// ---- دخول وتفعيل: Anonymous (العميل لسه معندوش توكن) + Rate Limit ضد تخمين الباسوورد والـ Tokens ----
[ApiController]
[Route("api/v{version:apiVersion}/portal/auth")]
[Asp.Versioning.ApiVersion("1.0")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class PortalAuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public PortalAuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivatePortalAccountCommand command)
        => Ok(await _mediator.Send(command));

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] PortalLoginCommand command)
        => Ok(await _mediator.Send(command));
}

// ---- بيانات العميل: Scheme الـ Portal + Role ClientPortal بس. توكن الفريق مش بيعدي هنا ----
[ApiController]
[Route("api/v{version:apiVersion}/portal")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = PortalAuth.Scheme, Roles = PortalAuth.Role)]
public class PortalController : ControllerBase
{
    private readonly IMediator _mediator;
    public PortalController(IMediator mediator) => _mediator = mediator;

    [HttpGet("me")]
    public async Task<IActionResult> Me() => Ok(await _mediator.Send(new GetPortalMeQuery()));

    [HttpGet("projects")]
    public async Task<IActionResult> Projects() => Ok(await _mediator.Send(new GetPortalProjectsQuery()));

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices() => Ok(await _mediator.Send(new GetPortalInvoicesQuery()));

    [HttpGet("invoices/{id:guid}")]
    public async Task<IActionResult> Invoice(Guid id) => Ok(await _mediator.Send(new GetPortalInvoiceQuery(id)));
}
