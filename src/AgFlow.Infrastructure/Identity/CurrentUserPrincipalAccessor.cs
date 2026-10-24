using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Infrastructure.Identity;

public class CurrentUserPrincipalAccessor : ICurrentUserPrincipalAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserPrincipalAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ClaimsPrincipal User =>
        _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
}
