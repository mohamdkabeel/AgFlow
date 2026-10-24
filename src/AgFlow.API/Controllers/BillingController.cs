using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Billing.Commands.GenerateDraftInvoice;
using AgFlow.Application.Modules.Billing.Commands.ApproveOverage;
using AgFlow.Application.Modules.Billing.Queries.GetClientProfitability;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/billing")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(Roles = "Owner,Admin,Finance")] // FR-BI-2: Finance فقط يشوف/يدير الفوترة
public class BillingController : ControllerBase
{
    private readonly IMediator _mediator;
    public BillingController(IMediator mediator) => _mediator = mediator;

    [HttpPost("invoices/generate-draft")]
    public async Task<IActionResult> GenerateDraftInvoice([FromBody] GenerateDraftInvoiceCommand command)
    {
        var invoiceId = await _mediator.Send(command);
        return CreatedAtAction(nameof(GenerateDraftInvoice), new { id = invoiceId }, new { id = invoiceId });
    }

    [HttpPost("overage/approve")]
    public async Task<IActionResult> ApproveOverage([FromBody] ApproveOverageCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    // US-4.3 — أهم Endpoint في المنتج كله
    [HttpGet("clients/{clientId}/profitability")]
    public async Task<IActionResult> GetClientProfitability(Guid clientId, [FromQuery] int year, [FromQuery] int month)
    {
        var result = await _mediator.Send(new GetClientProfitabilityQuery(clientId, year, month));
        return Ok(result);
    }
}
