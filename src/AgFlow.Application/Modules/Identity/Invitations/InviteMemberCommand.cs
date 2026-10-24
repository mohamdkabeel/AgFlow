using System.Net;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Application.Modules.Subscriptions.Services;
using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Invitations.Commands.InviteMember;

public record InviteMemberCommand(string Email, WorkspaceRole Role, decimal HourlyCost)
    : IRequest<InviteMemberResult>;

// عمداً مفيش Token في الرد: لو رجّعناه للـ Owner، يقدر يقبل الدعوة بدل صاحب الإيميل،
// وإثبات ملكية الإيميل (الفكرة كلها) يضيع. الـ Token بيروح للإيميل بس.
public record InviteMemberResult(Guid InvitationId, bool EmailSent);

public class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum().NotEqual(WorkspaceRole.Owner)
            .WithMessage("Members cannot be invited as Owner.");
        RuleFor(x => x.HourlyCost).InclusiveBetween(0, 10_000);
    }
}

public class InviteMemberCommandHandler : IRequestHandler<InviteMemberCommand, InviteMemberResult>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private const int MaxInvitationsPerDay = 50; // حماية من استخدام النظام لإرسال Spam بإسمنا

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly PlanLimitService _planLimits;
    private readonly UserManager<User> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InviteMemberCommandHandler> _logger;

    public InviteMemberCommandHandler(
        IApplicationDbContext dbContext, ICurrentUserService currentUser, ITokenService tokenService,
        IEmailService emailService, PlanLimitService planLimits, UserManager<User> userManager,
        IConfiguration configuration, ILogger<InviteMemberCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _tokenService = tokenService;
        _emailService = emailService;
        _planLimits = planLimits;
        _userManager = userManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<InviteMemberResult> Handle(InviteMemberCommand request, CancellationToken ct)
    {
        var workspaceId = _currentUser.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");
        var inviterId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("No user context found.");

        // منع تصعيد الصلاحيات: Admin ما يقدرش يعمل Admin تاني. ده قرار الـ Owner بس
        var inviterIsOwner = string.Equals(_currentUser.Role, "Owner", StringComparison.OrdinalIgnoreCase);
        if (request.Role == WorkspaceRole.Admin && !inviterIsOwner)
            throw new UnauthorizedAccessException("Only the Owner can invite an Admin.");

        var email = request.Email.Trim().ToLowerInvariant();

        // لو الإيميل ده عضو نشط فعلاً في الـ Workspace، مفيش داعي لدعوة
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            var alreadyActive = await _dbContext.WorkspaceMembers.AnyAsync(
                m => m.UserId == existingUser.Id && m.Status == MembershipStatus.Active, ct);
            if (alreadyActive)
                throw new DomainException("This person is already an active member of the workspace.");
        }

        var since = DateTime.UtcNow.AddHours(-24);
        var sentToday = await _dbContext.Invitations.CountAsync(i => i.CreatedAt >= since, ct);
        if (sentToday >= MaxInvitationsPerDay)
            throw new DomainException("Daily invitation limit reached. Please try again tomorrow.");

        if (!await _planLimits.CanAddSeatAsync(workspaceId, email))
            throw new DomainException("Your plan's seat limit has been reached. Upgrade to invite more members.");

        // إعادة الدعوة (Resend) = إلغاء القديمة وإنشاء جديدة بـ Token جديد. القديمة ماتفضلش صالحة
        var previous = await _dbContext.Invitations
            .Where(i => i.Email == email && i.Status == InvitationStatus.Pending)
            .ToListAsync(ct);
        foreach (var old in previous) old.Revoke();

        var rawToken = _tokenService.GenerateRefreshTokenValue(); // 64 بايت عشوائي، نفس جودة الـ Refresh Token
        var invitation = Invitation.Create(
            workspaceId, email, request.Role, request.HourlyCost,
            _tokenService.HashToken(rawToken), inviterId, InvitationLifetime);

        _dbContext.Invitations.Add(invitation);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new DomainException("An invitation for this email is already being created. Please retry.");
        }

        var emailSent = await TrySendEmailAsync(workspaceId, email, rawToken, ct);
        return new InviteMemberResult(invitation.Id, emailSent);
    }

    private async Task<bool> TrySendEmailAsync(Guid workspaceId, string email, string rawToken, CancellationToken ct)
    {
        try
        {
            var workspaceName = await _dbContext.Workspaces
                .Where(w => w.Id == workspaceId).Select(w => w.Name).FirstAsync(ct);

            var baseUrl = (_configuration["App:BaseUrl"] ?? throw new InvalidOperationException(
                "App:BaseUrl is not configured.")).TrimEnd('/');
            var link = $"{baseUrl}/accept-invite?token={Uri.EscapeDataString(rawToken)}";

            // SendGridEmailService بيبعت الـ Body كـ HTML كمان، واسم الـ Workspace كتبه أي مستخدم مسجّل.
            // من غير Encode، حد يسجّل Workspace باسم فيه HTML ويبعت "دعوة" Phishing من دومين AgFlow الحقيقي.
            var safeName = WebUtility.HtmlEncode(workspaceName);
            var body = $"<p>You've been invited to join <strong>{safeName}</strong> on AgFlow.</p>" +
                       $"<p><a href=\"{link}\">Accept the invitation</a></p>" +
                       "<p>This link expires in 7 days. If you weren't expecting it, ignore this email.</p>";

            await _emailService.SendAsync(email, $"You're invited to join {workspaceName} on AgFlow", body);
            return true;
        }
        catch (Exception ex)
        {
            // الدعوة اتحفظت. فشل الإيميل مايلغيش الشغل: الـ Owner يعيد الدعوة. بس لازم نسجّله ونقوله
            _logger.LogError(ex, "Failed to send invitation email for workspace {WorkspaceId}", workspaceId);
            return false;
        }
    }
}
