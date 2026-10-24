using System.Security.Claims;

namespace AgFlow.Application.Common.Interfaces;

// ICurrentUserService (Phase 8, Step 5) بيدينا قيم مفردة (WorkspaceId, UserId...)
// لكن IAuthorizationService.AuthorizeAsync محتاج الـ ClaimsPrincipal الكامل نفسه
public interface ICurrentUserPrincipalAccessor
{
    ClaimsPrincipal User { get; }
}
