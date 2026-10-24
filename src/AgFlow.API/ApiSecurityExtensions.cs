using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace AgFlow.API;

public static class PortalAuth
{
    public const string Scheme = "Portal";
    public const string Role = "ClientPortal";
}

public static class ApiSecurityExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

        var portalSecret = configuration["PortalJwt:Secret"]
            ?? throw new InvalidOperationException("PortalJwt:Secret is not configured.");
        // لو السرّين نفس القيمة، الفصل بين الفريق والعميل بقى شكلي (نفس المفتاح = توكن يعدي على الاتنين)
        if (portalSecret == secret)
            throw new InvalidOperationException("PortalJwt:Secret must be different from Jwt:Secret.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(secret)),
                    ValidateLifetime = true,
                    // الافتراضي 5 دقايق، يعني Token الـ 15 دقيقة بيعيش 20 فعلياً!
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            })
            // === Scheme منفصل للـ Client Portal ===
            // مفتاح + Audience مختلفين: Endpoint الفريق (Scheme الافتراضي) بيرفض توكن العميل بسبب التوقيع.
            // ده السبب إن [Authorize] العادي (زي GET /clients) مايفتحش لعميل خارجي حتى لو حد نسي يقفله بـ Role
            .AddJwtBearer(PortalAuth.Scheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["PortalJwt:Audience"] ?? "agflow-portal",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(portalSecret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        return services;
    }

    // ثغرة Audit: كان مفيش Rate Limiting على Login/Register
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // ملحوظة: ورا Azure App Service لازم تفعّل ForwardedHeaders،
                    // وإلا كل المستخدمين هيظهروا بنفس الـ IP بتاع الـ Proxy ويتشاركوا الحد
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });
        return services;
    }
}
