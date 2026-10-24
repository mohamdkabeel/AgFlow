using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Identity;

public record RegisterOwnerRequest(
    string Email,
    string Password,
    string FullName,
    string WorkspaceName,
    string Subdomain);

public record RegisterOwnerResult(bool Succeeded, string? ErrorMessage, Guid? WorkspaceId);

public class RegistrationService
{
    private readonly UserManager<User> _userManager;
    private readonly IApplicationDbContext _dbContext;

    public RegistrationService(UserManager<User> userManager, IApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<RegisterOwnerResult> RegisterAsync(RegisterOwnerRequest request)
    {
        // بنبدأ الـ Transaction هنا — من اللحظة دي، لو أي خطوة فشلت،
        // كل حاجة هترجع زي ما كانت وكأن حاجة معملتش خالص
        var subdomain = request.Subdomain.Trim().ToLowerInvariant();
        if (await _dbContext.Workspaces.AnyAsync(w => w.Subdomain == subdomain))
            return new RegisterOwnerResult(false, "This subdomain is already taken.", null);

        await _dbContext.BeginTransactionAsync();

        try
        {
            // الخطوة 1: إنشاء الـ User عبر ASP.NET Identity
            var user = new User
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                IsActive = true
            };

            var createUserResult = await _userManager.CreateAsync(user, request.Password);
            if (!createUserResult.Succeeded)
            {
                await _dbContext.RollbackTransactionAsync();
                var errors = string.Join(", ", createUserResult.Errors.Select(e => e.Description));
                return new RegisterOwnerResult(false, errors, null);
            }

            // الخطوة 2: إنشاء الـ Workspace (باستخدام الـ Factory Method من Phase 7)
            var workspace = Workspace.Create(request.WorkspaceName, subdomain);
            _dbContext.Workspaces.Add(workspace);

            // FR-SB-1: كل Workspace جديد يبدأ Trial 21 يوم تلقائياً (كان ناقص، Subscription.StartTrial ما كانتش بتتنادى من أي مكان)
            _dbContext.Subscriptions.Add(
                AgFlow.Domain.Modules.Subscriptions.Entities.Subscription.StartTrial(workspace.Id));

            // الخطوة 3: ربطهم عن طريق WorkspaceMember بـ Role = Owner
            var membership = WorkspaceMember.CreateOwner(workspace.Id, user.Id);
            _dbContext.WorkspaceMembers.Add(membership);

            await _dbContext.SaveChangesAsync();

            // كل حاجة نجحت → نأكد الـ Transaction فعلياً
            await _dbContext.CommitTransactionAsync();

            return new RegisterOwnerResult(true, null, workspace.Id);
        }
        catch (Exception)
        {
            // أي خطأ غير متوقع (مش بس من Identity) → Rollback برضه
            await _dbContext.RollbackTransactionAsync();
            throw;
        }
    }
}
