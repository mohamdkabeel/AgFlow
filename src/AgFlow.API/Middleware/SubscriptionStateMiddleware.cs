using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AgFlow.Application.Common.Exceptions;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Subscriptions.Entities;

namespace AgFlow.API.Middleware;

// علّم بيه أي Action لازم تشتغل حتى في وضع القراءة فقط (مثلاً: بدء الدفع للترقية، تصدير بيانات)
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AllowWhenReadOnlyAttribute : Attribute { }

// FR-SB-5 / US-5.3: بعد انتهاء الـ Trial، البيانات تفضل محفوظة وتتقري، بس مفيش كتابة جديدة.
//
// ليه Middleware على الـ HTTP Verb، مش علامة على كل Command؟
//  - "Fail Closed": أي Endpoint كتابة جديد (POST/PUT/DELETE/PATCH) بيتمنع تلقائياً. لو الحماية
//    اختيارية (Opt-in) هننسى نحط العلامة على Command جديد في يوم، وده بيعدي كتابة من Workspace غير مدفوع من غير ما حد يلاحظ.
//  - المنع الاستثنائي (AllowWhenReadOnly) هو اللي لازم نكتبه بإيدينا ونفكر فيه.
//
// Anonymous requests (Login, Refresh, Stripe Webhook, قبول دعوة) مش بتتفحص: مفيش Workspace في الـ Token.
public class SubscriptionStateMiddleware
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS" };

    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    // مكان واحد للمفتاح: الـ Webhook بيمسحه أول ما العميل يدفع عشان الكتابة تفتح فوراً (مش بعد 30 ثانية)
    public static string CacheKey(Guid workspaceId) => $"sub-write-blocked:{workspaceId}";

    private readonly RequestDelegate _next;
    public SubscriptionStateMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context, ICurrentUserService currentUser, IApplicationDbContext db,
        IMemoryCache cache, ILogger<SubscriptionStateMiddleware> logger)
    {
        if (SafeMethods.Contains(context.Request.Method)
            || context.User.Identity?.IsAuthenticated != true
            || context.GetEndpoint()?.Metadata.GetMetadata<AllowWhenReadOnlyAttribute>() is not null
            || currentUser.WorkspaceId is not Guid workspaceId)
        {
            await _next(context);
            return;
        }

        // الـ Key لازم فيه WorkspaceId. من غيره Workspace مدفوع ممكن ياخد نتيجة Workspace منتهي (وبالعكس)
        var cacheKey = CacheKey(workspaceId);
        if (!cache.TryGetValue(cacheKey, out bool blocked))
        {
            var sub = await db.Subscriptions.AsNoTracking()
                .Where(s => s.WorkspaceId == workspaceId)
                .Select(s => new { s.Status, s.TrialEndsAt })
                .FirstOrDefaultAsync(context.RequestAborted);

            // مفيش Subscription = بيانات غير متسقة (التسجيل بيعملها دايماً). Fail Closed ونسجّل
            if (sub is null)
                logger.LogError("Workspace {WorkspaceId} has no subscription; treating as read-only", workspaceId);

            blocked = sub is null || Subscription.IsWriteBlocked(sub.Status, sub.TrialEndsAt, DateTime.UtcNow);
            // 30 ثانية: بعد ما العميل يدفع، أقصى انتظار 30 ثانية لحد ما الكتابة تفتح (بدل Query على كل طلب كتابة)
            cache.Set(cacheKey, blocked, CacheDuration);
        }

        if (blocked)
            throw new SubscriptionReadOnlyException();

        await _next(context);
    }
}
