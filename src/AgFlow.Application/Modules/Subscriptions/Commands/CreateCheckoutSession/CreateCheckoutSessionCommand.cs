using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Application.Modules.Subscriptions.Services;
using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.Subscriptions.Entities;

namespace AgFlow.Application.Modules.Subscriptions.Commands.CreateCheckoutSession;

// بيستقبل "اسم" الخطة بس. الـ Price Id مش بييجي من الـ Client أبداً: لو جه منه، حد يبعت Price Id
// لخطة رخيصة (أو Id تجريبي) ويدفع أقل. السعر الفعلي بيتحدد هنا من إعدادات السيرفر
public record CreateCheckoutSessionCommand(string PlanName) : IRequest<CheckoutSessionResult>;
public record CheckoutSessionResult(string CheckoutUrl);

public class CreateCheckoutSessionCommandValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionCommandValidator()
    {
        RuleFor(x => x.PlanName).NotEmpty()
            .Must(p => PlanLimits.MaxSeats.ContainsKey(p))
            .WithMessage($"Plan must be one of: {string.Join(", ", PlanLimits.MaxSeats.Keys)}.");
    }
}

public class CreateCheckoutSessionCommandHandler
    : IRequestHandler<CreateCheckoutSessionCommand, CheckoutSessionResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPaymentService _payments;
    private readonly IConfiguration _configuration;

    public CreateCheckoutSessionCommandHandler(
        IApplicationDbContext db, ICurrentUserService currentUser,
        IPaymentService payments, IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _payments = payments;
        _configuration = configuration;
    }

    public async Task<CheckoutSessionResult> Handle(CreateCheckoutSessionCommand request, CancellationToken ct)
    {
        var workspaceId = _currentUser.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("No user context found.");

        var sub = await _db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId, ct)
            ?? throw new KeyNotFoundException("Subscription not found.");

        // اشتراك شغال (أو متأخر لسه بيتحصّل) + Checkout جديد = Stripe يعمل اشتراك تاني والعميل يتحاسب مرتين.
        // تغيير الخطة لمشترك حالي بيتم من Stripe Customer Portal (مش بنبنيه دلوقتي)
        if (sub.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue)
            throw new DomainException(
                "This workspace already has a paid subscription. Manage it from the billing portal.");

        var priceId = _configuration[$"Stripe:Prices:{request.PlanName}"];
        if (string.IsNullOrWhiteSpace(priceId))
            // مشكلة إعدادات عندنا (مش خطأ المستخدم)، فـ InvalidOperationException (500) مقصودة
            throw new InvalidOperationException($"Stripe price for plan '{request.PlanName}' is not configured.");

        var email = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var url = await _payments.CreateCheckoutSessionAsync(
            workspaceId, request.PlanName, priceId, email, sub.StripeCustomerId);

        return new CheckoutSessionResult(url);
    }
}
