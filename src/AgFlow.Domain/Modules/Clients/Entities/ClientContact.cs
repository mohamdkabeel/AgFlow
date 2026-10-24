using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Clients.Entities;

public class ClientContact
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    private ClientContact() { }

    internal static ClientContact Create(Guid clientId, string fullName, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Contact email cannot be empty.", nameof(email));

        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Contact name cannot be empty.", nameof(fullName));

        return new ClientContact
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant()
        };
    }

    internal void Update(string fullName, string email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Contact name cannot be empty.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Contact email cannot be empty.", nameof(email));

        FullName = fullName.Trim();
        Email = email.Trim().ToLowerInvariant();
    }
}
