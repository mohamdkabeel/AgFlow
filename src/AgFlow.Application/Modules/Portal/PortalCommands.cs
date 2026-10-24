using System.Net;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgFlow.Application.Common.Exceptions;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.Application.Modules.Portal;

// ======================= دعوة Contact للـ Portal (موظف: FR-CM-5) =======================
public record InvitePortalContactCommand(Guid ClientId, Guid ContactId) : IRequest<InvitePortalResult>;
public record InvitePortalResult(bool EmailSent);

public class InvitePortalContactCommandHandler : IRequestHandler<InvitePortalContactCommand, InvitePortalResult>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InvitePortalContactCommandHandler> _logger;

    public InvitePortalContactCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, ITokenService tokenService,
        IEmailService email, IConfiguration configuration, ILogger<InvitePortalContactCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _tokenService = tokenService;
        _email = email;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<InvitePortalResult> Handle(InvitePortalContactCommand request, CancellationToken ct)
    {
        var workspaceId = _currentUser.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        var client = await _db.Clients.Include(c => c.Contacts)
            .FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
            ?? throw new KeyNotFoundException("Client not found.");
        var contact = client.Contacts.FirstOrDefault(c => c.Id == request.ContactId)
            ?? throw new KeyNotFoundException("Contact not found.");

        if (client.Status != ClientStatus.Active)
            throw new DomainException("Cannot invite a contact of an archived client.");

        // الإيميل = هوية الدخول، فلازم يكون لعميل واحد بس في الـ Workspace (وإلا مين الـ Client اللي نفتحله؟).
        // قيد معروف: شخص بيمثّل شركتين عند نفس الـ Agency محتاج إيميل لكل شركة (Unique Index بيضمن ده كمان)
        var email = contact.Email;
        var clash = await _db.PortalCredentials.AnyAsync(c => c.Email == email && c.ClientContactId != contact.Id, ct);
        if (clash)
            throw new DomainException("This email already has portal access for another client in this workspace.");

        var rawToken = _tokenService.GenerateRefreshTokenValue();
        var tokenHash = _tokenService.HashToken(rawToken);

        var credential = await _db.PortalCredentials.FirstOrDefaultAsync(c => c.ClientContactId == contact.Id, ct);
        if (credential is null)
            _db.PortalCredentials.Add(ClientPortalCredential.Invite(
                workspaceId, client.Id, contact.Id, email, tokenHash, TokenLifetime));
        else
            credential.IssueActivationToken(tokenHash, TokenLifetime);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new DomainException("Portal access for this contact is already being set up. Please retry.");
        }

        var sent = await TrySendAsync(workspaceId, email, rawToken, ct);
        return new InvitePortalResult(sent);
    }

    private async Task<bool> TrySendAsync(Guid workspaceId, string email, string rawToken, CancellationToken ct)
    {
        try
        {
            var ws = await _db.Workspaces.Where(w => w.Id == workspaceId)
                .Select(w => new { w.Name, w.Subdomain }).FirstAsync(ct);
            var baseUrl = (_configuration["App:PortalBaseUrl"] ?? _configuration["App:BaseUrl"]
                ?? throw new InvalidOperationException("App:BaseUrl is not configured.")).TrimEnd('/');
            var link = $"{baseUrl}/portal/activate?workspace={Uri.EscapeDataString(ws.Subdomain)}&token={Uri.EscapeDataString(rawToken)}";

            // الـ Body بيتبعت كـ HTML: اسم الـ Workspace كتبه مستخدم، فلازم Encode (نفس سبب دعوات الفريق)
            var body = $"<p><strong>{WebUtility.HtmlEncode(ws.Name)}</strong> invited you to their client portal on AgFlow.</p>" +
                       $"<p><a href=\"{link}\">Set your password and sign in</a></p>" +
                       "<p>This link expires in 7 days and works once. If you weren't expecting it, ignore this email.</p>";
            await _email.SendAsync(email, $"{ws.Name} invited you to their client portal", body);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send portal invitation email for workspace {WorkspaceId}", workspaceId);
            return false;
        }
    }
}

// ======================= تعطيل وصول Contact =======================
public record DisablePortalAccessCommand(Guid ClientId, Guid ContactId) : IRequest;

public class DisablePortalAccessCommandHandler : IRequestHandler<DisablePortalAccessCommand>
{
    private readonly IApplicationDbContext _db;
    public DisablePortalAccessCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DisablePortalAccessCommand request, CancellationToken ct)
    {
        // ClientId في الشرط عمداً: Contact Id لوحده كان يسمح بتعطيل Credential عميل تاني (الـ Route بيقول عميل والـ Id لغيره)
        var credential = await _db.PortalCredentials
            .FirstOrDefaultAsync(c => c.ClientContactId == request.ContactId && c.ClientId == request.ClientId, ct)
            ?? throw new KeyNotFoundException("Portal access not found.");

        credential.Disable();
        await _db.SaveChangesAsync(ct);
    }
}

// ======================= تفعيل الحساب (Anonymous) =======================
public record ActivatePortalAccountCommand(string Token, string Password) : IRequest<ActivatePortalResult>;
public record ActivatePortalResult(string WorkspaceSubdomain);

public class ActivatePortalAccountCommandValidator : AbstractValidator<ActivatePortalAccountCommand>
{
    public ActivatePortalAccountCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty()
            .MinimumLength(10).MaximumLength(128) // الحد الأقصى: الـ Hashing البطيء مايبقاش وسيلة DoS
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}

public class ActivatePortalAccountCommandHandler : IRequestHandler<ActivatePortalAccountCommand, ActivatePortalResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPortalPasswordHasher _hasher;

    public ActivatePortalAccountCommandHandler(
        IApplicationDbContext db, ITokenService tokenService, IPortalPasswordHasher hasher)
    {
        _db = db;
        _tokenService = tokenService;
        _hasher = hasher;
    }

    public async Task<ActivatePortalResult> Handle(ActivatePortalAccountCommand request, CancellationToken ct)
    {
        // IgnoreQueryFilters موثّق: Anonymous (مفيش Token/Workspace). الأمان من الـ Token العشوائي (512 bit) نفسه
        var hash = _tokenService.HashToken(request.Token);
        var credential = await _db.PortalCredentials.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ActivationTokenHash == hash, ct);

        // رسالة واحدة لكل الحالات (مش موجود / منتهي / اتستخدم)
        if (credential is null || !credential.CanActivate(DateTime.UtcNow))
            throw new ArgumentException("Activation link is invalid or has expired.");

        var subdomain = await _db.Workspaces.Where(w => w.Id == credential.WorkspaceId)
            .Select(w => w.Subdomain).FirstAsync(ct);

        credential.Activate(_hasher.Hash(request.Password), DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);

        return new ActivatePortalResult(subdomain);
    }
}

// ======================= تسجيل الدخول (Anonymous) =======================
// WorkspaceSubdomain مطلوب لأن نفس إيميل العميل ممكن يكون عند أكتر من Agency (Workspace) على AgFlow
public record PortalLoginCommand(string WorkspaceSubdomain, string Email, string Password) : IRequest<PortalAuthResult>;
public record PortalAuthResult(string AccessToken, int ExpiresInSeconds);

public class PortalLoginCommandValidator : AbstractValidator<PortalLoginCommand>
{
    public PortalLoginCommandValidator()
    {
        RuleFor(x => x.WorkspaceSubdomain).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class PortalLoginCommandHandler : IRequestHandler<PortalLoginCommand, PortalAuthResult>
{
    private static string? _dummyHash;

    private readonly IApplicationDbContext _db;
    private readonly IPortalPasswordHasher _hasher;
    private readonly IPortalTokenService _tokens;

    public PortalLoginCommandHandler(IApplicationDbContext db, IPortalPasswordHasher hasher, IPortalTokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<PortalAuthResult> Handle(PortalLoginCommand request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var subdomain = request.WorkspaceSubdomain.Trim().ToLowerInvariant();
        var email = request.Email.Trim().ToLowerInvariant();

        var workspaceId = await _db.Workspaces.AsNoTracking()
            .Where(w => w.Subdomain == subdomain).Select(w => (Guid?)w.Id).FirstOrDefaultAsync(ct);

        // IgnoreQueryFilters موثّق: Anonymous، ومفلتر صراحةً بـ WorkspaceId اللي لقيناه من الـ Subdomain
        ClientPortalCredential? credential = workspaceId is null ? null
            : await _db.PortalCredentials.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.Email == email, ct);

        // === منع User Enumeration بالتوقيت ===
        // لو الحساب مش موجود/مش مفعّل/مقفول ورجعنا فوراً، الرد أسرع بكتير (مفيش Hashing) من حساب موجود بباسوورد غلط،
        // فمهاجم يعرف أنهي إيميلات عملاء الـ Agency بقياس الوقت. فبنعمل Hashing وهمي في كل مسار فاشل
        var canCheck = credential is { CanLogin: true } c1 && !c1.IsLockedOut(now);
        var passwordOk = canCheck
            ? _hasher.Verify(credential!.PasswordHash!, request.Password)
            : DummyVerify(request.Password);

        if (credential is not null && canCheck && !passwordOk)
        {
            credential.RegisterFailedLogin(now);
            await _db.SaveChangesAsync(ct);
        }

        if (!canCheck || !passwordOk)
            throw new InvalidCredentialsException();

        // العميل اتأرشف بعد ما اتدعى؟ مفيش دخول
        var clientActive = await _db.Clients.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(c => c.Id == credential!.ClientId && c.WorkspaceId == workspaceId
                && c.Status == ClientStatus.Active, ct);
        if (!clientActive)
            throw new InvalidCredentialsException();

        credential!.RegisterSuccessfulLogin(now);
        await _db.SaveChangesAsync(ct);

        var token = _tokens.GenerateAccessToken(credential.ClientContactId, credential.WorkspaceId, credential.ClientId);
        return new PortalAuthResult(token.Token, token.ExpiresInSeconds);
    }

    private bool DummyVerify(string password)
    {
        _dummyHash ??= _hasher.Hash("dummy-password-used-only-for-timing");
        _hasher.Verify(_dummyHash, password);
        return false;
    }
}
