using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Members;

public record ListMembersQuery : IRequest<IReadOnlyList<MemberDto>>;

// HourlyCost nullable: بيانات مالية، بتظهر لـ Owner/Admin/Finance بس
public record MemberDto(Guid MemberId, Guid UserId, string FullName, string Email,
    WorkspaceRole Role, MembershipStatus Status, DateTime JoinedAt, decimal? HourlyCost);

public class ListMembersQueryHandler : IRequestHandler<ListMembersQuery, IReadOnlyList<MemberDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public ListMembersQueryHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<MemberDto>> Handle(ListMembersQuery request, CancellationToken ct)
    {
        var role = _currentUser.Role;
        var canSeeCost = role is not null &&
            (role.Equals("Owner", StringComparison.OrdinalIgnoreCase)
             || role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
             || role.Equals("Finance", StringComparison.OrdinalIgnoreCase));

        // البداية من WorkspaceMembers (عليه Query Filter) وبعدين Join مع Users، مش العكس أبداً:
        // لو بدأنا من Users هنشوف مستخدمي كل الـ Workspaces
        // والـ Select بياخد الأعمدة المطلوبة بس، فـ PasswordHash مابيتقراش من الـ DB أصلاً
        var rows = await (
            from m in _dbContext.WorkspaceMembers.AsNoTracking()
            join u in _dbContext.Users.AsNoTracking() on m.UserId equals u.Id
            orderby u.FullName, m.Id
            select new { m.Id, m.UserId, u.FullName, u.Email, m.Role, m.Status, m.JoinedAt, m.HourlyCost })
            .Take(500)
            .ToListAsync(ct);

        return rows.Select(r => new MemberDto(
            r.Id, r.UserId, r.FullName, r.Email ?? string.Empty, r.Role, r.Status, r.JoinedAt,
            canSeeCost ? r.HourlyCost : null)).ToList();
    }
}
