using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AgFlow.Application.Modules.Identity;
using AgFlow.Application.Modules.Identity.Auth;

namespace AgFlow.API.Controllers;

// كله AllowAnonymous: المستخدم هنا لسه معاهوش Access Token صالح (أو انتهى).
// الحماية بتيجي من: Rate Limiting + Lockout + Hash للـ Refresh Token.
[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[Asp.Versioning.ApiVersion("1.0")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly RegistrationService _registration;

    public AuthController(IMediator mediator, RegistrationService registration)
    {
        _mediator = mediator;
        _registration = registration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterOwnerRequest request)
    {
        var result = await _registration.RegisterAsync(request);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, new { workspaceId = result.WorkspaceId })
            : BadRequest(new { message = result.ErrorMessage });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
        => Ok(await _mediator.Send(command));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        => Ok(await _mediator.Send(new RefreshTokenCommand(request.RefreshToken)));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _mediator.Send(new LogoutCommand(request.RefreshToken));
        return NoContent();
    }
}

public record RefreshRequest(string RefreshToken);
