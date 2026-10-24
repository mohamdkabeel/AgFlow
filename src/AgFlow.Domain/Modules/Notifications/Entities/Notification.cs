using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Notifications.Entities;

public class Notification : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Notification() { }

    public static Notification Create(Guid workspaceId, Guid recipientUserId, string message)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            RecipientUserId = recipientUserId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsRead() => IsRead = true;
}
