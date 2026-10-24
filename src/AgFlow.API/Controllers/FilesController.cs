using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Files.Commands.RequestUploadUrl;
using AgFlow.Application.Modules.Files.Commands.ConfirmUpload;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/files")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;
    public FilesController(IMediator mediator) => _mediator = mediator;

    [HttpPost("upload-url")]
    public async Task<IActionResult> RequestUploadUrl([FromBody] RequestUploadUrlCommand command)
        => Ok(await _mediator.Send(command));

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmUpload([FromBody] ConfirmFileUploadCommand command)
    {
        var fileId = await _mediator.Send(command);
        return CreatedAtAction(nameof(ConfirmUpload), new { id = fileId }, new { id = fileId });
    }
}
