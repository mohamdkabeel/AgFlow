namespace AgFlow.Application.Common.Interfaces;

public interface IPaymentService
{
    // بيرجع رابط صفحة الدفع بتاعت Stripe.
    // existingCustomerId: لو الـ Workspace كان عميل قبل كده (اشتراك اتلغى)، بنكمل على نفس الـ Customer
    // بدل ما نعمل واحد جديد ويتكرر في Stripe
    Task<string> CreateCheckoutSessionAsync(
        Guid workspaceId, string planName, string planPriceId, string customerEmail, string? existingCustomerId);
}
