using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;
using AgFlow.Domain.Modules.Notifications.Entities;
using AgFlow.Domain.Modules.Subscriptions.Entities;

namespace AgFlow.Application.Modules.Subscriptions.Jobs;

// US-5.2: الـ Owner لازم يتنبّه قبل ما الـ Trial يخلص، مش يتفاجئ بالـ Read-only.
// بيتشغّل يومياً. النافذة [بعد يومين، بعد 3 أيام) بتتتابع كل 24 ساعة من غير فجوات ولا تداخل،
// فكل Trial بيقع في نافذة واحدة بالظبط = تنبيه واحد (من غير ما نحتاج عمود "اتبعتله ولا لأ").
// لو الـ Job فاته يوم، التنبيه ده بيتفوّت: مقبول، لأن الـ Read-only نفسه مابيعتمدش على الـ Job.
public class TrialEndingReminderJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TrialEndingReminderJob> _logger;

    public TrialEndingReminderJob(
        IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<TrialEndingReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var now = DateTime.UtcNow;
        var from = now.AddDays(2);
        var to = now.AddDays(3);
        var baseUrl = (_configuration["App:BaseUrl"] ?? "").TrimEnd('/');

        List<(Guid WorkspaceId, DateTime TrialEndsAt)> expiring;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            // IgnoreQueryFilters موثّق: الـ Job شغال نيابة عن كل الـ Workspaces (مفيش مستخدم/Token)
            var rows = await db.Subscriptions.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Status == SubscriptionStatus.Trialing && s.TrialEndsAt >= from && s.TrialEndsAt < to)
                .Select(s => new { s.WorkspaceId, s.TrialEndsAt })
                .ToListAsync();
            expiring = rows.Select(r => (r.WorkspaceId, r.TrialEndsAt)).ToList();
        }

        foreach (var (workspaceId, trialEndsAt) in expiring)
        {
            // Scope جديد لكل Workspace: فشل واحد (إيميل مرفوض، SaveChanges) مايوقفش الباقي، ومايسيبش Entities فاسدة متتبعة
            using var scope = _scopeFactory.CreateScope();
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var owner = await (
                    from m in db.WorkspaceMembers.IgnoreQueryFilters()
                    where m.WorkspaceId == workspaceId && m.Role == WorkspaceRole.Owner && m.Status == MembershipStatus.Active
                    join u in db.Users on m.UserId equals u.Id
                    select new { m.UserId, u.Email }).FirstOrDefaultAsync();
                if (owner is null) continue;

                var daysLeft = Math.Max(1, (int)Math.Ceiling((trialEndsAt - now).TotalDays));
                var message = $"Your AgFlow trial ends in {daysLeft} days. After that your workspace becomes read-only until you choose a plan.";

                // إشعار داخل التطبيق أولاً (مضمون: نفس الـ DB)، وبعده الإيميل (ممكن يفشل)
                db.Notifications.Add(Notification.Create(workspaceId, owner.UserId, message));
                await db.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(owner.Email))
                    await email.SendAsync(owner.Email, "Your AgFlow trial is ending soon",
                        $"<p>{message}</p><p><a href=\"{baseUrl}/billing\">Choose a plan</a></p>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Trial reminder failed for WorkspaceId {WorkspaceId}", workspaceId);
            }
        }
    }
}
