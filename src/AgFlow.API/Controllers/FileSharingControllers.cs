using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Files.Sharing;
using AgFlow.Application.Modules.Portal;
using AgFlow.Domain.Modules.Files.Entities;

namespace AgFlow.API.Controllers;

// ---- الموظفين: FilesController الأصلي ما اتلمسش ----
[ApiController]
[Route("api/v{version:apiVersion}/files")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
[Tags("Files")]
public class FileSharingController : ControllerBase
{
    private readonly IMediator _mediator;
    public FileSharingController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid taskItemId)
        => Ok(await _mediator.Send(new ListTaskFilesQuery(taskItemId)));

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> AddVersion(Guid id, [FromBody] AddVersionRequest request)
        => Ok(new { versionNumber = await _mediator.Send(new AddFileVersionCommand(id, request.BlobPath)) });

    // FR-FM-4: مشاركة مع العميل. قرار إداري، مش لأي TeamMember
    [HttpPost("{id:guid}/request-approval")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> RequestApproval(Guid id)
    {
        await _mediator.Send(new RequestFileApprovalCommand(id));
        return NoContent();
    }

    [HttpGet("{id:guid}/download-url")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] int? version)
        => Ok(await _mediator.Send(new GetFileDownloadUrlQuery(id, version)));
}

public record AddVersionRequest(string BlobPath);

// ---- العميل (Portal scheme + Role ClientPortal بس) ----
[ApiController]
[Route("api/v{version:apiVersion}/portal/files")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize(AuthenticationSchemes = PortalAuth.Scheme, Roles = PortalAuth.Role)]
[Tags("Portal")]
public class PortalFilesController : ControllerBase
{
    private readonly IMediator _mediator;
    public PortalFilesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ApprovalStatus? status)
        => Ok(await _mediator.Send(new ListPortalFilesQuery(status)));

    [HttpGet("{id:guid}/download-url")]
    public async Task<IActionResult> Download(Guid id)
        => Ok(await _mediator.Send(new GetPortalFileDownloadQuery(id)));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] PortalApproveRequest request)
    {
        await _mediator.Send(new ApprovePortalFileCommand(id, request.VersionNumber));
        return NoContent();
    }

    [HttpPost("{id:guid}/request-revision")]
    public async Task<IActionResult> RequestRevision(Guid id, [FromBody] PortalRevisionRequest request)
    {
        await _mediator.Send(new RequestPortalFileRevisionCommand(id, request.VersionNumber, request.Note));
        return NoContent();
    }
}

public record PortalApproveRequest(int VersionNumber);
public record PortalRevisionRequest(int VersionNumber, string Note);
