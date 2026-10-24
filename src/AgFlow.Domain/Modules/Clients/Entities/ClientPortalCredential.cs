using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Clients.Entities;

// هوية دخول Client Portal: منفصلة تماماً عن User (الفريق الداخلي). السبب (FR-RP-6): العميل حدود ثقة مختلفة،
// وأي ربط بينه وبين جداول/Roles الفريق بيفتح باب إنه يوصل لبيانات داخلية بالغلط.
// الشخص نفسه هو ClientContact (الاسم والإيميل)، والـ Credential هو "مفتاح الدخول" بس.
public class ClientPortalCredential : ITenantScoped
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid ClientId { get; private set; }         // ثابت: بيتحط في الـ Token، مابيتغيّرش أبداً
    public Guid ClientContactId { get; private set; }
    public string Email { get; private set; } = string.Empty; // نسخة lowercase: Unique Index على (WorkspaceId, Email)

    // null لحد ما الشخص يفعّل حسابه بالرابط اللي في الإيميل
    public string? PasswordHash { get; private set; }

    // زي دعوات الفريق: الـ Token الخام في الإيميل بس، والـ Hash هو المخزّن
    public string? ActivationTokenHash { get; private set; }
    public DateTime? ActivationExpiresAt { get; private set; }

    public bool IsEnabled { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndsAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private ClientPortalCredential() { }

    public static ClientPortalCredential Invite(
        Guid workspaceId, Guid clientId, Guid clientContactId, string email,
        string activationTokenHash, TimeSpan tokenLifetime)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return new ClientPortalCredential
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClientId = clientId,
            ClientContactId = clientContactId,
            Email = email.Trim().ToLowerInvariant(),
            ActivationTokenHash = activationTokenHash,
            ActivationExpiresAt = DateTime.UtcNow.Add(tokenLifetime),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    // إعادة إرسال الدعوة / إعادة ضبط الباسوورد (بقرار موظف): بيفعّل الحساب لو كان متعطّل.
    // الباسوورد القديم بيفضل شغال لحد ما صاحب الحساب يفعّل بالرابط الجديد
    public void IssueActivationToken(string activationTokenHash, TimeSpan tokenLifetime)
    {
        ActivationTokenHash = activationTokenHash;
        ActivationExpiresAt = DateTime.UtcNow.Add(tokenLifetime);
        IsEnabled = true;
    }

    public bool CanActivate(DateTime utcNow) =>
        IsEnabled && ActivationTokenHash is not null && ActivationExpiresAt is { } exp && utcNow < exp;

    public void Activate(string passwordHash, DateTime utcNow)
    {
        if (!CanActivate(utcNow))
            throw new DomainException("Activation link is invalid or has expired.");

        PasswordHash = passwordHash;
        ActivationTokenHash = null;   // استخدام واحد بس
        ActivationExpiresAt = null;
        FailedLoginCount = 0;
        LockoutEndsAt = null;
    }

    public bool IsLockedOut(DateTime utcNow) => LockoutEndsAt is { } end && utcNow < end;

    public bool CanLogin => IsEnabled && PasswordHash is not null;

    public void RegisterFailedLogin(DateTime utcNow)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLogins)
        {
            LockoutEndsAt = utcNow.Add(LockoutDuration);
            FailedLoginCount = 0;
        }
    }

    public void RegisterSuccessfulLogin(DateTime utcNow)
    {
        FailedLoginCount = 0;
        LockoutEndsAt = null;
        LastLoginAt = utcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        ActivationTokenHash = null;
        ActivationExpiresAt = null;
    }
}
