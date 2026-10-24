using MediatR;
using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Application.Modules.Subscriptions.Services;

namespace AgFlow.Application.Modules.Subscriptions.Queries;

public record GetSubscriptionStatusQuery : IRequest<SubscriptionStatusDto>;

// الـ Frontend بيستخدمه لـ: بانر "باقي X يوم من الـ Trial"، وتحذير حد المقاعد (US-5.2)، ووضع القراءة فقط
public record SubscriptionStatusDto(
    string PlanName, string Status, bool IsReadOnly,
    DateTime? TrialEndsAt, int? TrialDaysLeft, int SeatsUsed, int? SeatLimit);

public class GetSubscriptionStatusQueryHandler : IRequestHandler<GetSubscriptionStatusQuery, SubscriptionStatusDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PlanLimitService _planLimits;

    public GetSubscriptionStatusQueryHandler(
        IApplicationDbContext db, ICurrentUserService currentUser, PlanLimitService planLimits)
    {
        _db = db;
        _currentUser = currentUser;
        _planLimits = planLimits;
    }

    public async Task<SubscriptionStatusDto> Handle(GetSubscriptionStatusQuery request, CancellationToken ct)
    {
        var workspaceId = _currentUser.WorkspaceId
            ?? throw new UnauthorizedAccessException("No workspace context found.");

        var sub = await _db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId, ct)
            ?? throw new KeyNotFoundException("Subscription not found.");

        var now = DateTime.UtcNow;
        var isTrial = sub.Status == Domain.Modules.Subscriptions.Entities.SubscriptionStatus.Trialing;
        int? daysLeft = isTrial ? Math.Max(0, (int)Math.Ceiling((sub.TrialEndsAt - now).TotalDays)) : null;

        var seats = await _planLimits.GetSeatUsageAsync(workspaceId);

        return new SubscriptionStatusDto(
            sub.PlanName, sub.EffectiveStatusAt(now).ToString(), sub.IsReadOnlyAt(now),
            isTrial ? sub.TrialEndsAt : null, daysLeft, seats.Used, seats.Limit);
    }
}
