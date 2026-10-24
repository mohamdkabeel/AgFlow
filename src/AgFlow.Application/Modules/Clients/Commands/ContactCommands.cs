using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.Clients.Contacts;

// ---- مساعد: بنحمّل الـ Client (Aggregate Root) بكل Contacts بتاعته ----
// كل تعديل على Contact بيعدي من الـ Client نفسه (مش من DbSet منفصل للـ Contacts)،
// عشان قواعد زي "إيميل مش مكرر" و"حد أقصى 20" تتطبق من مكان واحد
internal static class ClientLoader
{
    public static async Task<AgFlow.Domain.Modules.Clients.Entities.Client> LoadWithContactsAsync(
        IApplicationDbContext db, Guid clientId, CancellationToken ct)
        => await db.Clients.Include(c => c.Contacts).FirstOrDefaultAsync(c => c.Id == clientId, ct)
           ?? throw new KeyNotFoundException("Client not found.");
}

public record AddContactCommand(Guid ClientId, string FullName, string Email) : IRequest<Guid>;
public record UpdateContactCommand(Guid ClientId, Guid ContactId, string FullName, string Email) : IRequest;
public record RemoveContactCommand(Guid ClientId, Guid ContactId) : IRequest;
public record ListContactsQuery(Guid ClientId) : IRequest<IReadOnlyList<ContactDto>>;
public record ContactDto(Guid Id, string FullName, string Email);

public class AddContactCommandValidator : AbstractValidator<AddContactCommand>
{
    public AddContactCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class UpdateContactCommandValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.ContactId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class AddContactCommandHandler : IRequestHandler<AddContactCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    public AddContactCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(AddContactCommand request, CancellationToken ct)
    {
        var client = await ClientLoader.LoadWithContactsAsync(_db, request.ClientId, ct);
        var contact = client.AddContact(request.FullName, request.Email);
        await _db.SaveChangesAsync(ct);
        return contact.Id;
    }
}

public class UpdateContactCommandHandler : IRequestHandler<UpdateContactCommand>
{
    private readonly IApplicationDbContext _db;
    public UpdateContactCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(UpdateContactCommand request, CancellationToken ct)
    {
        var client = await ClientLoader.LoadWithContactsAsync(_db, request.ClientId, ct);

        // الإيميل هو هوية دخول الـ Portal. تغييره بصمت = حد تاني (إيميل جديد) يورث حساب الشخص القديم.
        // لازم تعطّل/تحذف وصوله الأول وتدعو الإيميل الجديد من جديد
        var existing = client.Contacts.FirstOrDefault(x => x.Id == request.ContactId);
        if (existing is not null
            && !string.Equals(existing.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase)
            && await _db.PortalCredentials.AnyAsync(x => x.ClientContactId == request.ContactId, ct))
            throw new AgFlow.Domain.Common.DomainException(
                "This contact has portal access. Remove the contact and add a new one to change the email.");

        client.UpdateContact(request.ContactId, request.FullName, request.Email);
        await _db.SaveChangesAsync(ct);
    }
}

public class RemoveContactCommandHandler : IRequestHandler<RemoveContactCommand>
{
    private readonly IApplicationDbContext _db;
    public RemoveContactCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RemoveContactCommand request, CancellationToken ct)
    {
        var client = await ClientLoader.LoadWithContactsAsync(_db, request.ClientId, ct);
        client.RemoveContact(request.ContactId);

        // حذف الشخص = حذف مفتاح دخوله (مش بس تعطيله): مفيش حساب يتيم بيتجاهله الـ Guard
        var credential = await _db.PortalCredentials.FirstOrDefaultAsync(x => x.ClientContactId == request.ContactId, ct);
        if (credential is not null)
            _db.PortalCredentials.Remove(credential);
        await _db.SaveChangesAsync(ct);
    }
}

public class ListContactsQueryHandler : IRequestHandler<ListContactsQuery, IReadOnlyList<ContactDto>>
{
    private readonly IApplicationDbContext _db;
    public ListContactsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ContactDto>> Handle(ListContactsQuery request, CancellationToken ct)
    {
        // Client عليه Query Filter، فالـ Where على Id بتاعه هو اللي بيضمن إننا جوه نفس الـ Workspace.
        // (ClientContact نفسه مش ITenantScoped، فمنقراش منه مباشرة أبداً)
        return await _db.Clients
            .AsNoTracking()
            .Where(c => c.Id == request.ClientId)
            .SelectMany(c => c.Contacts)
            .OrderBy(c => c.FullName).ThenBy(c => c.Id)
            .Select(c => new ContactDto(c.Id, c.FullName, c.Email))
            .ToListAsync(ct);
    }
}
