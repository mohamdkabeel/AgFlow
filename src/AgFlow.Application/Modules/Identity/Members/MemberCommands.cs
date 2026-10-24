using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Members;

// ---- مساعد مشترك: مين اللي بينفّذ، وهل مسموح له يلمس العضو ده؟ ----
internal static class MemberPolicy
{
    public static bool IsOwner(string? role) =>
        string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase);

    // Admin ما يقدرش يلمس Admin تاني (دور ولا تعطيل): ده قرار الـ Owner بس.
    // من غير القاعدة دي، Admin واحد مخترق/ساخط يقدر يعطّل باقي الـ Admins ويستولي على الـ Workspace
    public static void EnsureCanManage(string? actorRole, WorkspaceMember target)
    {
        if (target.Role == WorkspaceRole.Admin && !IsOwner(actorRole))
            throw new UnauthorizedAccessException("Only the Owner can manage an Admin.");
    }
}

// ======================= تغيير الدور =======================
public record ChangeMemberRoleCommand(Guid MemberId, WorkspaceRole Role) : IRequest;

public class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum().NotEqual(WorkspaceRole.Owner);
    }
}

public class ChangeMemberRoleCommandHandler : IRequestHandler<ChangeMemberRoleCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ChangeMemberRoleCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task Handle(ChangeMemberRoleCommand request, CancellationToken ct)
    {
        var member = await _dbContext.WorkspaceMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct)
            ?? throw new KeyNotFoundException("Member not found.");

        if (member.UserId == _currentUser.UserId)
            throw new DomainException("You cannot change your own role.");

        MemberPolicy.EnsureCanManage(_currentUser.Role, member);
        // ترقية لـ Admin = تصعيد صلاحيات، فنفس القاعدة
        if (request.Role == WorkspaceRole.Admin && !MemberPolicy.IsOwner(_currentUser.Role))
            throw new UnauthorizedAccessException("Only the Owner can promote a member to Admin.");

        member.ChangeRole(request.Role);
        await _dbContext.SaveChangesAsync(ct);
        // الدور الجديد بيبان في الـ Token الجاي (Refresh أو Login). القديم صالح لحد 15 دقيقة (NFR-S3)
    }
}

// ======================= تعديل التكلفة =======================
public record UpdateMemberCostCommand(Guid MemberId, decimal HourlyCost) : IRequest;

public class UpdateMemberCostCommandValidator : AbstractValidator<UpdateMemberCostCommand>
{
    public UpdateMemberCostCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.HourlyCost).InclusiveBetween(0, 10_000);
    }
}

public class UpdateMemberCostCommandHandler : IRequestHandler<UpdateMemberCostCommand>
{
    private readonly IApplicationDbContext _dbContext;
    public UpdateMemberCostCommandHandler(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task Handle(UpdateMemberCostCommand request, CancellationToken ct)
    {
        var member = await _dbContext.WorkspaceMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct)
            ?? throw new KeyNotFoundException("Member not found.");

        // مفيش EnsureCanManage هنا عمداً: الـ Owner لازم يتحدد له تكلفة ساعة (الافتراضي 0 بيضخّم الربحية)،
        // والتكلفة مش صلاحية إدارية. وبتأثر على الإدخالات "الجديدة" بس، القديمة ليها Snapshot (FR-TT-3)
        member.UpdateHourlyCost(request.HourlyCost);
        await _dbContext.SaveChangesAsync(ct);
    }
}

// ======================= تعطيل عضو =======================
public record DeactivateMemberCommand(Guid MemberId) : IRequest;

public class DeactivateMemberCommandHandler : IRequestHandler<DeactivateMemberCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public DeactivateMemberCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task Handle(DeactivateMemberCommand request, CancellationToken ct)
    {
        var workspaceId = _currentUser.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        var member = await _dbContext.WorkspaceMembers.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct)
            ?? throw new KeyNotFoundException("Member not found.");

        if (member.UserId == _currentUser.UserId)
            throw new DomainException("You cannot deactivate yourself.");

        MemberPolicy.EnsureCanManage(_currentUser.Role, member);
        member.Deactivate(); // مش بنحذف: ساعاته وفواتيره وتاريخه لازم يفضلوا (Phase 7 Q)

        // نلغي الـ Refresh Tokens بتاعته في الـ Workspace ده بس (ممكن يكون عضو في Workspace تاني)،
        // عشان ما يقدرش يجدد الجلسة. الـ Access Token الحالي يفضل صالح لحد 15 دقيقة (JWT مش بيتلغي)
        var tokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == member.UserId && t.WorkspaceId == workspaceId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens) token.Revoke();

        await _dbContext.SaveChangesAsync(ct);
    }
}
