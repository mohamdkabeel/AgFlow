using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Subscriptions.Commands.CreateCheckoutSession;
using AgFlow.Application.Modules.Subscriptions.Queries;
using AgFlow.API.Middleware;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/subscription")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(Roles = "Owner,Admin")]
public class SubscriptionController : ControllerBase
{
    private readonly IMediator _mediator;
    public SubscriptionController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetStatus() => Ok(await _mediator.Send(new GetSubscriptionStatusQuery()));

    public record CheckoutRequest(string PlanName);

    // Owner بس: ده قرار دفع. و[AllowWhenReadOnly] لأن ده بالظبط اللي بيفتح الـ Workspace المقفول
    [HttpPost("checkout")]
    [Authorize(Roles = "Owner")]
    [AllowWhenReadOnly]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        => Ok(await _mediator.Send(new CreateCheckoutSessionCommand(request.PlanName)));
}
