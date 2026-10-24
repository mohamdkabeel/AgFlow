using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Infrastructure.Identity;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(User user, Guid workspaceId, WorkspaceRole role)
    {
        // الـ Claims هي "المعلومات" اللي هتتحط جوه الـ Token نفسه
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),

            // أهم Claim في المشروع كله — ده اللي هنستخرج منه WorkspaceId
            // لاحقاً في الـ Global Query Filter (Phase 5, Step 3)
            new("workspaceId", workspaceId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };

        var key = new SymmetricSecurityKey(
            Convert.FromBase64String(_configuration["Jwt:Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15), // زي NFR-S3
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes); // 64 حرف
    }

    public string GenerateRefreshTokenValue()
    {
        // Randomness قوي أمنياً (Cryptographically secure)، مش Guid.NewGuid() عادي
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }
}
