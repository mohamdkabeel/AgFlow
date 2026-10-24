namespace AgFlow.Domain.Modules.Identity.Entities;

// ملحوظة: مقصود إنها مش ITenantScoped. الـ Refresh بيحصل لما الـ Access Token خلص،
// يعني مفيش WorkspaceId Claim وقتها، والـ Global Filter كان هيرجع صفر نتائج.
// بنخزن WorkspaceId كبيانات عادية عشان نعرف نطلع Token للـ Workspace الصح.
public class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty; // SHA-256 hex، مش الـ Token نفسه
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, Guid workspaceId, string tokenHash, TimeSpan lifetime)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkspaceId = workspaceId,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(lifetime)
        };
    }

    public void Revoke()
    {
        if (RevokedAt is null)
            RevokedAt = DateTime.UtcNow;
    }
}
