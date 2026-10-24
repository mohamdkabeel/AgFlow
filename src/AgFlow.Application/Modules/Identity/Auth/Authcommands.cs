using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Exceptions;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Identity.Auth;

public record AuthResult(string AccessToken, string RefreshToken, int ExpiresInSeconds);

internal static class AuthSettings
{
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    public const int AccessTokenSeconds = 15 * 60; // لازم يطابق JwtTokenService (NFR-S3)
}

// بيصدر Access + Refresh Token ويحفظ الـ Refresh (Hash بس) — مشترك بين Login و Refresh
internal static class TokenIssuing
{
    public static async Task<AuthResult> IssueAsync(
        User user, Guid workspaceId, WorkspaceRole role,
        ITokenService tokenService, IApplicationDbContext db, CancellationToken ct)
    {
        var accessToken = tokenService.GenerateAccessToken(user, workspaceId, role);
        var refreshValue = tokenService.GenerateRefreshTokenValue();

        db.RefreshTokens.Add(RefreshToken.Create(
            user.Id, workspaceId, tokenService.HashToken(refreshValue), AuthSettings.RefreshTokenLifetime));
        await db.SaveChangesAsync(ct);

        return new AuthResult(accessToken, refreshValue, AuthSettings.AccessTokenSeconds);
    }
}

// ======================= Login =======================
public record LoginCommand(string Email, string Password, Guid? WorkspaceId) : IRequest<AuthResult>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(
        UserManager<User> userManager, IApplicationDbContext dbContext, ITokenService tokenService)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive)
            throw new InvalidCredentialsException();

        // Lockout: بعد 5 محاولات غلط (الافتراضي) الحساب بيتقفل 5 دقايق — حماية من Brute Force
        if (await _userManager.IsLockedOutAsync(user))
            throw new InvalidCredentialsException();

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            throw new InvalidCredentialsException();
        }
        await _userManager.ResetAccessFailedCountAsync(user);

        // IgnoreQueryFilters موثّق: المستخدم لسه معاه مفيش Token، يعني مفيش WorkspaceId Claim،
        // والـ Global Filter كان هيرجع صفر. بنفلتر صراحةً بـ UserId اللي اتحقق منه بالباسوورد توّاً.
        var memberships = _dbContext.WorkspaceMembers
            .IgnoreQueryFilters()
            .Where(m => m.UserId == user.Id && m.Status == MembershipStatus.Active);

        if (request.WorkspaceId is Guid requested)
            memberships = memberships.Where(m => m.WorkspaceId == requested);

        // لو المستخدم عضو في أكتر من Workspace ومبعتش WorkspaceId، بناخد أقدم عضوية
        // (شاشة اختيار الـ Workspace قرار Frontend لاحقاً، والـ API جاهز بالـ WorkspaceId الاختياري)
        var membership = await memberships.OrderBy(m => m.JoinedAt).FirstOrDefaultAsync(ct);
        if (membership is null)
            throw new InvalidCredentialsException();

        return await TokenIssuing.IssueAsync(
            user, membership.WorkspaceId, membership.Role, _tokenService, _dbContext, ct);
    }
}

// ======================= Refresh (مع Rotation) =======================
public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResult>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(
        UserManager<User> userManager, IApplicationDbContext dbContext, ITokenService tokenService)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AuthResult> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = _tokenService.HashToken(request.RefreshToken);
        var stored = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
            throw new InvalidCredentialsException();

        // === Reuse Detection ===
        // Token اتلغى قبل كده وحد بيستخدمه تاني = غالباً اتسرق (السارق أو صاحبه استخدمه بعد الـ Rotation).
        // مش عارفين مين فيهم، فبنلغي كل Tokens المستخدم ده ويضطر يسجل دخول من جديد.
        if (stored.RevokedAt is not null)
        {
            var active = await _dbContext.RefreshTokens
                .Where(t => t.UserId == stored.UserId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var token in active) token.Revoke();
            await _dbContext.SaveChangesAsync(ct);
            throw new InvalidCredentialsException();
        }

        if (!stored.IsActive) // منتهي الصلاحية
            throw new InvalidCredentialsException();

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive)
            throw new InvalidCredentialsException();

        // بنتأكد إن العضوية لسه Active — ده بيعالج ثغرة الـ Audit: موظف اتعطل يفضل معاه Token 15 دقيقة بس
        var membership = await _dbContext.WorkspaceMembers
            .IgnoreQueryFilters() // موثّق: نفس سبب الـ Login، مفيش Claims وقت الـ Refresh
            .FirstOrDefaultAsync(m => m.UserId == stored.UserId
                && m.WorkspaceId == stored.WorkspaceId
                && m.Status == MembershipStatus.Active, ct);
        if (membership is null)
            throw new InvalidCredentialsException();

        stored.Revoke(); // Rotation: القديم يتلغي، والجديد يتصدر (بيتحفظ مع SaveChanges جوه IssueAsync)

        return await TokenIssuing.IssueAsync(
            user, membership.WorkspaceId, membership.Role, _tokenService, _dbContext, ct);
    }
}

// ======================= Logout =======================
public record LogoutCommand(string RefreshToken) : IRequest;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public LogoutCommandHandler(IApplicationDbContext dbContext, ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task Handle(LogoutCommand request, CancellationToken ct)
    {
        var hash = _tokenService.HashToken(request.RefreshToken);
        var stored = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        // Idempotent: لو التوكن مش موجود أو ملغي أصلاً، مفيش خطأ — النتيجة النهائية واحدة (المستخدم خرج)
        if (stored is not null)
        {
            stored.Revoke();
            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
