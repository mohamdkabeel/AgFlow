using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Common.Interfaces;

public interface ITokenService
{
    // بيرجع الـ Access Token الموقّع، فيه الـ Claims (UserId, WorkspaceId, Role)
    string GenerateAccessToken(User user, Guid workspaceId, WorkspaceRole role);

    // بيرجع Refresh Token عشوائي (نص عادي، مش JWT) — هنخزن الـ Hash بتاعه بس
    string GenerateRefreshTokenValue();

    // SHA-256: ينفع هنا (بعكس الباسوردات) لأن الـ Token عشوائي وطويل (512 bit)،
    // فمفيش داعي لـ Hash بطيء زي PBKDF2، والبحث بالـ Hash لازم يكون سريع وبـ Index
    string HashToken(string token);
}
