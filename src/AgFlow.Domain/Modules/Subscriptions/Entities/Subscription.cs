using AgFlow.Domain.Common;

namespace AgFlow.Domain.Modules.Subscriptions.Entities;

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    ReadOnly, // FR-SB-5: بدل الحذف الفوري
    Canceled
}

public class Subscription : ITenantScoped
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string PlanName { get; private set; } = string.Empty; // "Starter" / "Growth" / "AgencyPro"
    public SubscriptionStatus Status { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public DateTime TrialEndsAt { get; private set; }

    private Subscription() { }

    // FR-SB-1: كل Workspace جديد يبدأ Trial على Growth تلقائياً
    public static Subscription StartTrial(Guid workspaceId)
    {
        return new Subscription
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            PlanName = "Growth",
            Status = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(21) // القرار المعتمد في Phase 2
        };
    }

    // بيتنادى من الـ Webhook Handler لما Stripe يأكد الدفع
    public void ActivateFromStripe(string stripeCustomerId, string stripeSubscriptionId, string planName)
    {
        StripeCustomerId = stripeCustomerId;
        StripeSubscriptionId = stripeSubscriptionId;
        PlanName = planName;
        Status = SubscriptionStatus.Active;
    }

    // === الحالة "الفعلية" بتتحسب من الوقت، مش بس من العمود المخزّن ===
    // لو اعتمدنا على Job يحوّل Trialing → ReadOnly، أي تأخير أو فشل في الـ Job معناه Workspace منتهي
    // الـ Trial بيكتب بياناته ببلاش. الحساب اللحظي بيخلّي الـ Enforcement مايعتمدش على أي Job.
    // PastDue مسموح بالكتابة عمداً: Stripe بيعيد محاولة الدفع (Dunning)، وقفل العميل أول ما كارته يفشل مرة قرار قاسي
    public static bool IsWriteBlocked(SubscriptionStatus status, DateTime trialEndsAt, DateTime utcNow) =>
        status switch
        {
            SubscriptionStatus.ReadOnly or SubscriptionStatus.Canceled => true,
            SubscriptionStatus.Trialing => utcNow >= trialEndsAt,
            _ => false
        };

    public bool IsReadOnlyAt(DateTime utcNow) => IsWriteBlocked(Status, TrialEndsAt, utcNow);

    public SubscriptionStatus EffectiveStatusAt(DateTime utcNow) =>
        Status == SubscriptionStatus.Trialing && utcNow >= TrialEndsAt ? SubscriptionStatus.ReadOnly : Status;

    // بيترجم حالة اشتراك Stripe (اللي جاية من Webhook موقّع) لحالتنا.
    // unpaid/canceled/incomplete_expired = Stripe خلاص وقف التحصيل → Read-only (مش حذف: البيانات تفضل)
    public void SyncFromStripeStatus(string stripeStatus)
    {
        switch (stripeStatus)
        {
            case "active":
                Status = SubscriptionStatus.Active;
                break;
            case "past_due":
                Status = SubscriptionStatus.PastDue;
                break;
            case "canceled":
            case "unpaid":
            case "incomplete_expired":
                Status = SubscriptionStatus.Canceled;
                break;
            // trialing/incomplete/paused: مفيش تغيير (مش بنستخدم Trial بتاع Stripe، التجربة بتاعتنا)
        }
    }

    // FR-SB-5: Read-Only بدل القفل الكامل
    public void MoveToReadOnly() => Status = SubscriptionStatus.ReadOnly;
    public void MarkPastDue() => Status = SubscriptionStatus.PastDue;
    public void Cancel() => Status = SubscriptionStatus.Canceled;
}
