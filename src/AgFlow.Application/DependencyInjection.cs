using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using AgFlow.Application.Common.Authorization;
using AgFlow.Application.Modules.Billing.Jobs;
using AgFlow.Application.Modules.Identity;
using AgFlow.Application.Modules.Subscriptions.Jobs;
using AgFlow.Application.Modules.Subscriptions.Services;

namespace AgFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR هيدور تلقائياً على كل الـ Commands/Queries/Handlers
        // الموجودة في الـ Assembly ده ويسجلهم كلهم دفعة واحدة
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // بيضيف الـ ValidationBehavior كـ "محطة" تتنفذ قبل أي Handler
            cfg.AddOpenBehavior(typeof(Common.Behaviors.ValidationBehavior<,>));
        });

        // بيدور تلقائياً على كل كلاسات الـ FluentValidation ويسجلها
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<RegistrationService>();
        services.AddScoped<PlanLimitService>();

        // الـ Jobs بتتنشأ من الـ DI بتاع Hangfire، فلازم تتسجل. Transient: Instance جديد لكل تنفيذ
        services.AddTransient<RecurringInvoiceJob>();
        services.AddTransient<TrialEndingReminderJob>();

        // بدون التسجيل ده، IAuthorizationService.AuthorizeAsync مش هيلاقي الـ Handler،
        // وهيرجّع "فشل" لكل مستخدم من غير أي خطأ ظاهر (الأخطر: الفشل صامت)
        services.AddScoped<IAuthorizationHandler, TaskAssignmentAuthorizationHandler>();

        return services;
    }
}
