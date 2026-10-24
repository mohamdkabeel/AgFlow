using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Stripe;
using Stripe.Checkout;
using AgFlow.API.Middleware;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Application.Modules.Subscriptions.Services;

namespace AgFlow.API.Controllers;

// من غير [Authorize]: Stripe مش بيبعتلنا JWT. الحماية هي التحقق من "توقيع" الطلب بدل ده.
[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IApplicationDbContext dbContext, IConfiguration configuration,
        IMemoryCache cache, ILogger<StripeWebhookController> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken ct)
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            // أهم سطر أمني هنا: بيتأكد إن الطلب فعلاً من Stripe مش حد بيبعت POST مباشر
            // throwOnApiVersionMismatch:false: من غيره أي فرق بين إصدار مكتبة Stripe.net وإصدار حسابك
            // بيخلي كل Webhook يفشل في التحقق (والـ Stripe يفضل يعيد المحاولة ويقفل الـ Endpoint)
            stripeEvent = EventUtility.ConstructEvent(
                json, Request.Headers["Stripe-Signature"], webhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return BadRequest(); // التوقيع غلط، الطلب مرفوض
        }

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await OnCheckoutCompleted((Session)stripeEvent.Data.Object, ct);
                break;

            // بيغطي: فشل دفع (past_due)، تعافي (active)، إلغاء، عدم سداد (unpaid)
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                await OnSubscriptionChanged((Stripe.Subscription)stripeEvent.Data.Object, ct);
                break;
        }

        // 200 حتى للأحداث اللي مش مهتمين بيها: لو رجّعنا خطأ، Stripe بيعيد المحاولة أيام بدون فايدة
        return Ok();
    }

    private async Task OnCheckoutCompleted(Session session, CancellationToken ct)
    {
        // لو الـ Metadata ناقصة أو غلط، ده مش طلب بتاعنا. مانرميش Exception (500)
        // لأن Stripe هيعيد نفس الطلب الفاسد للأبد. بنسجّل ونرجّع 200
        if (session.Metadata is null
            || !session.Metadata.TryGetValue("workspaceId", out var wsRaw) || !Guid.TryParse(wsRaw, out var workspaceId)
            || !session.Metadata.TryGetValue("plan", out var plan) || !PlanLimits.MaxSeats.ContainsKey(plan))
        {
            _logger.LogError("Stripe checkout.session.completed {SessionId} has invalid metadata", session.Id);
            return;
        }

        // IgnoreQueryFilters موثّق: مفيش JWT (Stripe مش بيبعته). الـ WorkspaceId من Metadata
        // اتحط بإيدينا وقت إنشاء الـ Session، والطلب كله اتحقق من توقيعه فوق
        var subscription = await _dbContext.Subscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId, ct);
        if (subscription is null)
        {
            _logger.LogError("Stripe checkout for unknown workspace {WorkspaceId}", workspaceId);
            return;
        }

        // Idempotent: Stripe ممكن يبعت نفس الحدث أكتر من مرة، والتفعيل مرتين بنفس البيانات مفيش فيه ضرر
        subscription.ActivateFromStripe(session.CustomerId, session.SubscriptionId, plan);
        await _dbContext.SaveChangesAsync(ct);

        // نفتح الكتابة فوراً، بدل ما العميل يدفع ويفضل مقفول لحد 30 ثانية
        _cache.Remove(SubscriptionStateMiddleware.CacheKey(workspaceId));
    }

    private async Task OnSubscriptionChanged(Stripe.Subscription stripeSub, CancellationToken ct)
    {
        var subscription = await _dbContext.Subscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id, ct);
        if (subscription is null)
        {
            // ممكن يوصل قبل checkout.session.completed (الترتيب مش مضمون). Stripe هيبعت أحداث تانية، مفيش داعي لخطأ
            _logger.LogWarning("Stripe subscription event for unknown subscription {StripeSubscriptionId}", stripeSub.Id);
            return;
        }

        // الحدث "deleted" الـ Status فيه "canceled"، فنفس الترجمة بتشتغل للاتنين
        subscription.SyncFromStripeStatus(stripeSub.Status);
        await _dbContext.SaveChangesAsync(ct);
        _cache.Remove(SubscriptionStateMiddleware.CacheKey(subscription.WorkspaceId));
    }
}
