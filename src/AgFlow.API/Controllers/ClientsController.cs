using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgFlow.Application.Modules.Clients.Commands.CreateClient;
using AgFlow.Application.Modules.Clients.Commands.ArchiveClient;
using AgFlow.Application.Modules.Clients.Contacts;
using AgFlow.Application.Modules.Portal;
using AgFlow.Application.Modules.Clients.Commands.CreateRetainer;
using AgFlow.Application.Modules.Clients.Commands.UpdateClient;
using AgFlow.Application.Modules.Clients.Queries.GetClientById;
using AgFlow.Application.Modules.Clients.Queries.GetClientRetainers;
using AgFlow.Application.Modules.Clients.Queries.ListClients;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/clients")]
[Asp.Versioning.ApiVersion("1.0")]
[Authorize] // لازم يكون Logged in أصلاً (أي Role)
public class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClientsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // FR-CM-1: Owner/Admin/PM بس يقدروا ينشئوا Client
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> Create([FromBody] CreateClientCommand command)
    {
        // لاحظ: الـ Controller مش عارف حاجة عن الـ Validation ولا منطق الإنشاء —
        // بس بيبعت الأمر لـ MediatR ويستنى النتيجة
        var clientId = await _mediator.Send(command);

        return CreatedAtAction(nameof(GetById), new { id = clientId }, new { id = clientId });
    }

    // أي Role داخلي يقدر يشوف بيانات العميل (القراءة أوسع من الكتابة عادة)
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var client = await _mediator.Send(new GetClientByIdQuery(id));

        // لو null، يبقى إما مش موجود، أو (الأهم) بتاع Workspace تاني —
        // في الحالتين، الرد نفسه NotFound، من غير ما نفرّق بينهم للمستخدم
        // (تسريب معلومة "موجود بس مش بتاعك" في حد ذاته مشكلة أمنية صغيرة)
        if (client is null)
            return NotFound();

        return Ok(client);
    }

    // الـ Status الافتراضي Active. ابعت status=Archived لعرض الأرشيف
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ClientStatus? status = ClientStatus.Active, [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await _mediator.Send(new ListClientsQuery(status, search, page, pageSize)));

    public record UpdateClientRequest(string CompanyName);

    // الـ Id من الـ Route، والباقي من الـ Body: مفيش طريقة يبعت Id مختلف في الاتنين
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request)
    {
        await _mediator.Send(new UpdateClientCommand(id, request.CompanyName));
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Archive(Guid id)
    {
        await _mediator.Send(new ArchiveClientCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        await _mediator.Send(new ReactivateClientCommand(id));
        return NoContent();
    }

    // FR-CM-3: الأسعار والعقود Owner/Admin بس (والـ Finance للقراءة)
    public record CreateRetainerRequest(int MonthlyHours, decimal MonthlyPrice, bool RolloverEnabled, DateOnly StartDate);

    [HttpPost("{id:guid}/retainers")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> CreateRetainer(Guid id, [FromBody] CreateRetainerRequest request)
    {
        var retainerId = await _mediator.Send(new CreateRetainerCommand(
            id, request.MonthlyHours, request.MonthlyPrice, request.RolloverEnabled, request.StartDate));
        return CreatedAtAction(nameof(GetRetainers), new { id }, new { id = retainerId });
    }

    [HttpGet("{id:guid}/retainers")]
    [Authorize(Roles = "Owner,Admin,Finance")]
    public async Task<IActionResult> GetRetainers(Guid id)
        => Ok(await _mediator.Send(new GetClientRetainersQuery(id)));

    // ---- Contacts (FR-CM-2): Owner/Admin/PM يكتبوا، وأي Role داخلي يقرا ----
    public record ContactRequest(string FullName, string Email);

    [HttpGet("{id:guid}/contacts")]
    public async Task<IActionResult> ListContacts(Guid id)
        => Ok(await _mediator.Send(new ListContactsQuery(id)));

    [HttpPost("{id:guid}/contacts")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> AddContact(Guid id, [FromBody] ContactRequest request)
    {
        var contactId = await _mediator.Send(new AddContactCommand(id, request.FullName, request.Email));
        return CreatedAtAction(nameof(ListContacts), new { id }, new { id = contactId });
    }

    [HttpPut("{id:guid}/contacts/{contactId:guid}")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> UpdateContact(Guid id, Guid contactId, [FromBody] ContactRequest request)
    {
        await _mediator.Send(new UpdateContactCommand(id, contactId, request.FullName, request.Email));
        return NoContent();
    }

    [HttpDelete("{id:guid}/contacts/{contactId:guid}")]
    [Authorize(Roles = "Owner,Admin,ProjectManager")]
    public async Task<IActionResult> RemoveContact(Guid id, Guid contactId)
    {
        await _mediator.Send(new RemoveContactCommand(id, contactId));
        return NoContent();
    }

    // FR-CM-5: دعوة Contact للـ Portal (أو إعادة إرسال الدعوة / إعادة ضبط الباسوورد)
    [HttpPost("{id:guid}/contacts/{contactId:guid}/portal-invite")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> InvitePortal(Guid id, Guid contactId)
        => Ok(await _mediator.Send(new InvitePortalContactCommand(id, contactId)));

    [HttpPost("{id:guid}/contacts/{contactId:guid}/portal-disable")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> DisablePortal(Guid id, Guid contactId)
    {
        await _mediator.Send(new DisablePortalAccessCommand(id, contactId));
        return NoContent();
    }
}
