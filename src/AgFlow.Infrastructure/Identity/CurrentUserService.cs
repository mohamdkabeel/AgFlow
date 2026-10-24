using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Infrastructure.Identity;

public class CurrentUserService : ICurrentUserService
{
    // AsyncLocal بيفضل القيمة بتاعته "مرتبطة" بالـ async flow الحالي بس —
    // مش Static عادي (اللي كان هيتشارك غلط بين Requests/Jobs مختلفة شغالة
    // في نفس اللحظة)، ومش محتاج HttpContext خالص
    private static readonly AsyncLocal<Guid?> _ambientWorkspaceId = new();

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    // لو في Background Job عملنا RunAsWorkspace، بناخد منها الأول —
    // لو مفيش (الحالة العادية، HTTP Request)، نرجع للـ Claim زي ما كنا بنعمل بالظبط
    public Guid? WorkspaceId
    {
        get
        {
            if (_ambientWorkspaceId.Value is Guid ambientId)
                return ambientId;

            var value = User?.FindFirst("workspaceId")?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Role => User?.FindFirst(ClaimTypes.Role)?.Value;

    // تنفيذ الـ Method اللي الـ Interface عرّفها — دلوقتي Application بينادي
    // عليها عن طريق ICurrentUserService بس، من غير ما يعرف Infrastructure خالص
    public IDisposable RunAsWorkspace(Guid workspaceId)
    {
        _ambientWorkspaceId.Value = workspaceId;
        return new AmbientWorkspaceScope();
    }

    private class AmbientWorkspaceScope : IDisposable
    {
        public void Dispose() => _ambientWorkspaceId.Value = null;
    }
}
