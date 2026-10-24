using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Common.Csv;
using AgFlow.Application.Modules.Billing.Queries.GetClientProfitability;
using AgFlow.Application.Modules.Reports.Queries.Dashboard;
using AgFlow.Application.Modules.Reports.Queries.Utilization;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/reports")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportsController(IMediator mediator) => _mediator = mediator;

    // FR-RP-2: Owner/Admin/Finance بس: التقرير فيه تكلفة الموظفين (بيانات حساسة)
    // شهري: ?year=2026&month=9      ربع سنوي: ?year=2026&quarter=3
    [HttpGet("profitability")]
    [Authorize(Roles = "Owner,Admin,Finance")]
    public async Task<IActionResult> Profitability(
        [FromQuery] int year, [FromQuery] int? month, [FromQuery] int? quarter)
    {
        var query = BuildProfitabilityQuery(year, month, quarter, out var error);
        if (query is null) return BadRequest(new { message = error });
        return Ok(await _mediator.Send(query));
    }

    // FR-RP-4: نفس بيانات التقرير ونفس الصلاحيات، بصيغة CSV
    [HttpGet("profitability/csv")]
    [Authorize(Roles = "Owner,Admin,Finance")]
    public async Task<IActionResult> ProfitabilityCsv(
        [FromQuery] int year, [FromQuery] int? month, [FromQuery] int? quarter)
    {
        var query = BuildProfitabilityQuery(year, month, quarter, out var error);
        if (query is null) return BadRequest(new { message = error });

        var report = await _mediator.Send(query);
        var bytes = CsvBuilder.Build(
            new[] { "Client", "Revenue", "DraftRevenue", "Cost", "Profit", "MarginPercent", "TotalHours", "BillableApprovedHours" },
            report.Clients.Select(r => (IReadOnlyList<object?>)new object?[]
                { r.ClientName, r.Revenue, r.DraftRevenue, r.Cost, r.Profit, r.MarginPercent, r.TotalHours, r.BillableApprovedHours }));

        var label = month is { } m ? $"{year}-{m:D2}" : $"{year}-Q{quarter}";
        return File(bytes, "text/csv; charset=utf-8", $"profitability-{label}.csv");
    }

    // FR-RP-1: Owner/Admin
    [HttpGet("dashboard")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Dashboard() => Ok(await _mediator.Send(new GetDashboardQuery()));

    // FR-RP-3: PM (والـ Owner/Admin)
    [HttpGet("utilization")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> Utilization([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        => Ok(await _mediator.Send(new GetUtilizationReportQuery(from, to)));

    [HttpGet("utilization/csv")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> UtilizationCsv([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var report = await _mediator.Send(new GetUtilizationReportQuery(from, to));
        var bytes = CsvBuilder.Build(
            new[] { "Member", "LoggedHours", "BillableHours", "CapacityHours", "UtilizationPercent" },
            report.Members.Select(r => (IReadOnlyList<object?>)new object?[]
                { r.FullName, r.LoggedHours, r.BillableHours, r.CapacityHours, r.UtilizationPercent }));
        return File(bytes, "text/csv; charset=utf-8", $"utilization-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.csv");
    }

    private static GetProfitabilityReportQuery? BuildProfitabilityQuery(
        int year, int? month, int? quarter, out string? error)
    {
        error = null;
        if (year is < 2020 or > 2100) { error = "Invalid year."; return null; }
        if ((month is null) == (quarter is null)) { error = "Provide either month or quarter."; return null; }

        if (month is { } m)
        {
            if (m is < 1 or > 12) { error = "Month must be 1-12."; return null; }
            return new GetProfitabilityReportQuery(year, m, 1);
        }
        var q = quarter!.Value;
        if (q is < 1 or > 4) { error = "Quarter must be 1-4."; return null; }
        return new GetProfitabilityReportQuery(year, (q - 1) * 3 + 1, 3);
    }
}
