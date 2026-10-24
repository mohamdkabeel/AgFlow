using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Infrastructure.Identity;

public static class PortalClaims
{
    public const string TokenType = "token_type";
    public const string TokenTypeValue = "portal";
    public const string ContactId = "portalContactId"; // مش NameIdentifier عمداً: لو Principal العميل اتسرّب لكود الفريق، UserId يطلع null
    public const string ClientId = "clientId";
    public const string WorkspaceId = "workspaceId";   // نفس اسم Claim الفريق: الـ Query Filter يشتغل على Requests الـ Portal كمان
    public const string Role = "ClientPortal";
}

// نفس Hasher الـ Identity (PBKDF2 + Salt عشوائي + Iterations)، من غير ما نكتب Crypto بإيدينا
public class PortalPasswordHasher : IPortalPasswordHasher
{
    private readonly PasswordHasher<object> _inner = new();
    private static readonly object Subject = new();

    public string Hash(string password) => _inner.HashPassword(Subject, password);

    public bool Verify(string hash, string password) =>
        _inner.VerifyHashedPassword(Subject, hash, password) != PasswordVerificationResult.Failed;
}

public class PortalTokenService : IPortalTokenService
{
    // 30 دقيقة ومفيش Refresh Token في الـ v1: العميل بيدخل نادراً، وبالـ Guard بنفحص الحساب مفعّل في كل طلب.
    // لو لقينا إن إعادة الدخول كل نص ساعة مزعجة، بنضيف Refresh (نفس نمط الفريق)
    public const int LifetimeSeconds = 30 * 60;

    private readonly IConfiguration _configuration;
    public PortalTokenService(IConfiguration configuration) => _configuration = configuration;

    public PortalAccessToken GenerateAccessToken(Guid contactId, Guid workspaceId, Guid clientId)
    {
        var claims = new List<Claim>
        {
            new(PortalClaims.TokenType, PortalClaims.TokenTypeValue),
            new(PortalClaims.ContactId, contactId.ToString()),
            new(PortalClaims.ClientId, clientId.ToString()),
            new(PortalClaims.WorkspaceId, workspaceId.ToString()),
            new(ClaimTypes.Role, PortalClaims.Role)
        };

        // مفتاح توقيع مختلف عن مفتاح الفريق: مستحيل توكن عميل يعدي كتوكن موظف (والعكس)
        var key = new SymmetricSecurityKey(Convert.FromBase64String(_configuration["PortalJwt:Secret"]!));
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["PortalJwt:Audience"] ?? "agflow-portal",
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(LifetimeSeconds),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new PortalAccessToken(new JwtSecurityTokenHandler().WriteToken(token), LifetimeSeconds);
    }
}

public class CurrentPortalUser : ICurrentPortalUser
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentPortalUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public PortalIdentity? Identity
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            // لازم الـ Claim يقول "portal": مش بنثق في وجود Claims بأسماء معينة بس
            if (user?.FindFirst(PortalClaims.TokenType)?.Value != PortalClaims.TokenTypeValue)
                return null;

            return Guid.TryParse(user.FindFirst(PortalClaims.ContactId)?.Value, out var contactId)
                && Guid.TryParse(user.FindFirst(PortalClaims.ClientId)?.Value, out var clientId)
                && Guid.TryParse(user.FindFirst(PortalClaims.WorkspaceId)?.Value, out var workspaceId)
                ? new PortalIdentity(contactId, clientId, workspaceId)
                : null;
        }
    }
}
