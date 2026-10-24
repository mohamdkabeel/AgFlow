using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.TimeTracking.Commands.ApproveTimeEntries;
using AgFlow.Application.Modules.TimeTracking.Commands.LogTimeEntry;
using AgFlow.Application.Modules.TimeTracking.Queries.GetPendingTimeEntries;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/time-entries")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize] // أي Role يقدر يسجل وقت — مفيش تخصيص هنا لأن الـ FR ملهاش قيد Role معين
public class TimeEntriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TimeEntriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Log([FromBody] LogTimeEntryCommand command)
    {
        var timeEntryId = await _mediator.Send(command);
        return CreatedAtAction(nameof(Log), new { id = timeEntryId }, new { id = timeEntryId });
    }

    // Pending Queue للمراجعة — Role بس مش أي حد
    [HttpGet("pending")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> GetPending([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _mediator.Send(new GetPendingTimeEntriesQuery(page, pageSize)));

    [HttpPost("approve")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> Approve([FromBody] ApproveTimeEntriesCommand command)
    {
        var approved = await _mediator.Send(command);
        return Ok(new { approved });
    }
}
