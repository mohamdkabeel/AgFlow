using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Application.Modules.Billing.Commands.GenerateDraftInvoice;

namespace AgFlow.Application.Modules.Billing.Jobs;

// بيتجدول مرة في الشهر (أول يوم، 03:00 UTC). وحتى لو اتنفذ تاني، مفيش خطر تكرار
// لأن GenerateDraftInvoiceCommand نفسه Idempotent.
public class RecurringInvoiceJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringInvoiceJob> _logger;

    public RecurringInvoiceJob(IServiceScopeFactory scopeFactory, ILogger<RecurringInvoiceJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private record RetainerRef(Guid Id, Guid WorkspaceId);

    public async Task RunAsync()
    {
        // الفترة اللي بنفوترها = الشهر اللي فات. Retainer بدأ الشهر ده بيتفوتر الشهر الجاي
        var today = DateTime.UtcNow;
        var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
        var billingPeriodStart = firstOfThisMonth.AddMonths(-1);

        List<RetainerRef> retainers;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            retainers = await db.RetainerAgreements
                .IgnoreQueryFilters() // الـ Job شغال نيابة عن كل الـ Workspaces
                // مستحق للفترة: بدأ قبل نهايتها، وإما لسه شغال أو وقف بعد بدايتها (آخر شهر لازم يتفوتر)
                .Where(r => r.StartDate < firstOfThisMonth
                    && (r.IsActive || (r.EndDate != null && r.EndDate >= billingPeriodStart)))
                .Select(r => new RetainerRef(r.Id, r.WorkspaceId))
                .ToListAsync();
        }

        foreach (var retainer in retainers)
        {
            // Scope (وبالتالي DbContext) جديد لكل Retainer — ده مش تفصيلة:
            // في النسخة اللي فاتت كان DbContext واحد للكل. لو SaveChanges فشل في Retainer،
            // الـ Entities الفاشلة بتفضل متتبعة وبتتحاول تتحفظ تاني مع الـ Retainer اللي بعده،
            // فكل اللي بعدها كان هيفشل بنفس الخطأ، والـ try/catch اللي كتبناه كان بيخبي المشكلة بس.
            using var scope = _scopeFactory.CreateScope();
            try
            {
                var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
                using var _ = currentUser.RunAsWorkspace(retainer.WorkspaceId);

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new GenerateDraftInvoiceCommand(retainer.Id));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Recurring invoice failed for RetainerId {RetainerId} in WorkspaceId {WorkspaceId}",
                    retainer.Id, retainer.WorkspaceId);
            }
        }
    }
}
