using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;
using AgFlow.Infrastructure.Email;
using AgFlow.Infrastructure.Identity;
using AgFlow.Infrastructure.Payments;
using AgFlow.Infrastructure.Storage;
using AgFlow.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgFlow.Infrastructure;

// static class لأن الـ Extension Methods لازم تكون كده دايماً
public static class DependencyInjection
{
    // "this IServiceCollection services" ← ده اللي بيخلينا نكتب
    // services.AddInfrastructureServices(...) وكأنها method أصلية على IServiceCollection
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AgFlowDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // لما أي كود يطلب IApplicationDbContext (زي RegistrationService)،
        // ادّيله نفس النسخة بتاعت AgFlowDbContext اللي DI بيديرها في نفس الـ Request
        // (GetRequiredService بترجع نفس الـ Scoped instance، مش نسخة جديدة)
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<AgFlowDbContext>());

        // Ensure the required Identity services are available
        // AddIdentityCore بدل AddIdentity: الأخيرة بتسجل Cookie Authentication كـ Default Scheme،
        // فأي طلب غير مصرح بيه كان هيرجع 302 Redirect لصفحة Login بدل 401 — غلط لأي API
        services.AddIdentityCore<User>(o => o.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AgFlowDbContext>()
            .AddDefaultTokenProviders();

        // خدمة توليد الـ JWT / Refresh Token من Step 3
        services.AddScoped<ITokenService, JwtTokenService>();

        // Hangfire: Jobs محفوظة في نفس الـ SQL Server (جدول Schema اسمه HangFire بيتعمل تلقائياً).
        // لو السيرفر وقع أثناء Job، بيكمل لما يرجع، وده فرق Hangfire عن Timer عادي جوه التطبيق
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection")));
        services.AddHangfireServer();

        services.AddMemoryCache(); // بيستخدمه SubscriptionStateMiddleware

        // === الخدمات اللي كانت ناقصة (Interface في Application ← Implementation هنا) ===
        // ده تطبيق عملي لـ Dependency Inversion: الـ Application يعرف الـ Interface بس،
        // والـ DI Container هو الوحيد اللي يعرف مين التنفيذ الفعلي.

        // بيقرأ الـ UserId/WorkspaceId من الـ JWT (أو من الـ Ambient Workspace في الـ Background Jobs)
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentUserPrincipalAccessor, CurrentUserPrincipalAccessor>();

        // === Client Portal: هوية منفصلة عن الفريق ===
        services.AddSingleton<IPortalPasswordHasher, PortalPasswordHasher>();
        services.AddScoped<IPortalTokenService, PortalTokenService>();
        services.AddScoped<ICurrentPortalUser, CurrentPortalUser>();

        services.AddScoped<IFileStorageService, AzureBlobStorageService>();
        services.AddScoped<IEmailService, SendGridEmailService>();
        services.AddScoped<IPaymentService, StripePaymentService>();

        // StripePaymentService بيستخدم الـ Static API، فالمفتاح بيتحط مرة واحدة هنا.
        // لو مش موجود (بيئة تطوير من غير Stripe) مانكسرش التشغيل — هيفشل بس وقت أول استخدام فعلي
        var stripeKey = configuration["Stripe:SecretKey"];
        if (!string.IsNullOrWhiteSpace(stripeKey))
            Stripe.StripeConfiguration.ApiKey = stripeKey;

        return services;
    }
}
