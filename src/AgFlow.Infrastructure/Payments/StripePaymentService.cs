using Microsoft.Extensions.Configuration;
using Stripe.Checkout;
using AgFlow.Application.Common.Interfaces;

namespace AgFlow.Infrastructure.Payments;

public class StripePaymentService : IPaymentService
{
    private readonly string _baseUrl;

    public StripePaymentService(IConfiguration configuration)
    {
        // الروابط من الإعدادات مش مكتوبة في الكود: نفس الكود بيشتغل على Local / Staging / Production
        _baseUrl = (configuration["App:BaseUrl"]
            ?? throw new InvalidOperationException("App:BaseUrl is not configured.")).TrimEnd('/');
    }

    public async Task<string> CreateCheckoutSessionAsync(
        Guid workspaceId, string planName, string planPriceId, string customerEmail, string? existingCustomerId)
    {
        // WorkspaceId والخطة بيتحطوا في Metadata على الـ Session وعلى الـ Subscription نفسها،
        // عشان أي Webhook (checkout أو subscription.updated) يعرف يربط نفسه بالـ Workspace الصح
        var metadata = new Dictionary<string, string>
        {
            { "workspaceId", workspaceId.ToString() },
            { "plan", planName }
        };

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            ClientReferenceId = workspaceId.ToString(),
            LineItems = new List<SessionLineItemOptions> { new() { Price = planPriceId, Quantity = 1 } },
            Metadata = metadata,
            SubscriptionData = new SessionSubscriptionDataOptions { Metadata = metadata },
            SuccessUrl = $"{_baseUrl}/billing/success",
            CancelUrl = $"{_baseUrl}/billing/cancel"
        };

        // Stripe بيرفض الاتنين مع بعض: يا Customer موجود يا CustomerEmail
        if (!string.IsNullOrEmpty(existingCustomerId))
            options.Customer = existingCustomerId;
        else
            options.CustomerEmail = customerEmail;

        var session = await new SessionService().CreateAsync(options);
        return session.Url;
    }
}
