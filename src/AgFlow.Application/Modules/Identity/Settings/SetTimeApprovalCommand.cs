using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Application.Modules.Identity.Settings;

public record SetTimeApprovalCommand(bool Required) : IRequest;

public class SetTimeApprovalCommandHandler : IRequestHandler<SetTimeApprovalCommand>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public SetTimeApprovalCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task Handle(SetTimeApprovalCommand request, CancellationToken ct)
    {
        var workspaceId = _currentUser.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new KeyNotFoundException("Workspace not found.");

        // التغيير بيأثر على الإدخالات "الجديدة" بس. اللي Pending فعلاً بتفضل Pending لحد ما حد يوافق عليها،
        // لأن اعتماد فلوس تلقائياً بمجرد تغيير إعداد قرار مالي مش المفروض يحصل من غير علم حد
        workspace.SetTimeApprovalRequired(request.Required);
        await _dbContext.SaveChangesAsync(ct);
    }
}
