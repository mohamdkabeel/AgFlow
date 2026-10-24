using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Identity.Entities;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Revoked
}

// دعوة عضو فريق للانضمام لـ Workspace. الـ Token الخام بيتبعت بالإيميل بس، وإحنا بنخزن الـ Hash بتاعه
// (زي الـ Refresh Token): لو الـ DB اتسربت، مفيش حد يقدر يقبل دعوات بالنيابة عن غيره.
public class Invitation : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Email { get; private set; } = string.Empty; // دايماً lowercase
    public WorkspaceRole Role { get; private set; }
    public decimal HourlyCost { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid InvitedByUserId { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }

    private Invitation() { }

    public static Invitation Create(
        Guid workspaceId, string email, WorkspaceRole role, decimal hourlyCost,
        string tokenHash, Guid invitedByUserId, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (role == WorkspaceRole.Owner)
            throw new DomainException("Cannot invite a member as Owner.");
        if (hourlyCost < 0)
            throw new ArgumentException("Hourly cost cannot be negative.", nameof(hourlyCost));

        var now = DateTime.UtcNow;
        return new Invitation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            HourlyCost = hourlyCost,
            TokenHash = tokenHash,
            InvitedByUserId = invitedByUserId,
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.Add(lifetime)
        };
    }

    // "منتهية" مش حالة مخزّنة: بتتحسب من الوقت. لو خزّناها كحالة محتاجين Job يحدّثها، وده تعقيد من غير لازمة
    public bool IsUsable(DateTime utcNow) => Status == InvitationStatus.Pending && utcNow < ExpiresAt;

    public void Accept()
    {
        if (!IsUsable(DateTime.UtcNow))
            throw new DomainException("Invitation is invalid or has expired.");
        Status = InvitationStatus.Accepted;
        AcceptedAt = DateTime.UtcNow;
    }

    public void Revoke()
    {
        if (Status != InvitationStatus.Pending)
            throw new DomainException("Only a pending invitation can be revoked.");
        Status = InvitationStatus.Revoked;
    }
}
