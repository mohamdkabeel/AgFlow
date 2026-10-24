using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Clients.Entities;

public enum ClientStatus
{
    Active,
    Archived
}

public class Client : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string CompanyName { get; private set; } = string.Empty;
    public ClientStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<ClientContact> _contacts = new();
    public IReadOnlyCollection<ClientContact> Contacts => _contacts.AsReadOnly();

    private Client() { }

    // FR-CM-1: Owner/Admin/PM can create a Client
    public static Client Create(Guid workspaceId, string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name cannot be empty.", nameof(companyName));

        return new Client
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            CompanyName = companyName,
            Status = ClientStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    private const int MaxContacts = 20;

    // FR-CM-2: يمكن إضافة أكثر من Contact لنفس العميل
    public ClientContact AddContact(string fullName, string email)
    {
        if (Status == ClientStatus.Archived)
            throw new DomainException("Cannot add contacts to an archived client.");
        if (_contacts.Count >= MaxContacts)
            throw new DomainException($"A client can have at most {MaxContacts} contacts.");
        EnsureEmailIsUnique(email, exceptContactId: null);

        var contact = ClientContact.Create(Id, fullName, email);
        _contacts.Add(contact);
        return contact;
    }

    public void UpdateContact(Guid contactId, string fullName, string email)
    {
        var contact = _contacts.FirstOrDefault(c => c.Id == contactId)
            ?? throw new DomainException("Contact not found on this client.");
        EnsureEmailIsUnique(email, exceptContactId: contactId);
        contact.Update(fullName, email);
    }

    public void RemoveContact(Guid contactId)
    {
        var contact = _contacts.FirstOrDefault(c => c.Id == contactId)
            ?? throw new DomainException("Contact not found on this client.");
        _contacts.Remove(contact);
    }

    // Defense in Depth: الفحص ده + Unique Index على (ClientId, Email) في الـ Configuration
    private void EnsureEmailIsUnique(string email, Guid? exceptContactId)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (_contacts.Any(c => c.Email == normalized && c.Id != exceptContactId))
            throw new DomainException("A contact with this email already exists for this client.");
    }

    public void Rename(string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name cannot be empty.", nameof(companyName));
        // الفواتير القديمة مش بتتأثر: بتحتفظ بـ ClientNameSnapshot اللي اتثبت وقت إصدارها
        CompanyName = companyName.Trim();
    }

    // FR-CM-7: أرشفة بدل حذف نهائي
    public void Archive() => Status = ClientStatus.Archived;

    public void Reactivate() => Status = ClientStatus.Active;
}
