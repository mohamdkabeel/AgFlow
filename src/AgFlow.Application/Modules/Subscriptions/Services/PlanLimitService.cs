using Microsoft.EntityFrameworkCore;
using AgFlow.Application.Common.Interfaces;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Application.Modules.Subscriptions.Services;

public static class PlanLimits
{
    public static readonly Dictionary<string, int> MaxSeats = new()
    {
        { "Starter", 8 },
        { "Growth", 20 },
        { "AgencyPro", 50 }
    };
}

public class PlanLimitService
{
    private readonly IApplicationDbContext _dbContext;

    public PlanLimitService(IApplicationDbContext dbContext) => _dbContext = dbContext;

    public record SeatUsage(int Used, int? Limit);

    // Limit = null يعني مفيش حد أقصى (Enterprise / خطة مش في القائمة)
    // ignoringPendingEmail: لما بنعيد دعوة نفس الإيميل، الدعوة القديمة هتتلغى، فمانحسبهاش مرتين
    public async Task<SeatUsage> GetSeatUsageAsync(Guid workspaceId, string? ignoringPendingEmail = null)
    {
        var plan = await _dbContext.Subscriptions
            .Where(s => s.WorkspaceId == workspaceId)
            .Select(s => s.PlanName)
            .FirstOrDefaultAsync();

        int? limit = plan is not null && PlanLimits.MaxSeats.TryGetValue(plan, out var max) ? max : null;

        // المقعد بيتحسب للأعضاء النشطين بس (العضو المعطّل مابياخدش مقعد)، + الدعوات المعلقة (حجز مقعد)
        var now = DateTime.UtcNow;
        var email = ignoringPendingEmail?.Trim().ToLowerInvariant();

        var activeMembers = await _dbContext.WorkspaceMembers
            .CountAsync(m => m.WorkspaceId == workspaceId && m.Status == MembershipStatus.Active);
        var pendingInvites = await _dbContext.Invitations
            .CountAsync(i => i.WorkspaceId == workspaceId
                && i.Status == InvitationStatus.Pending && i.ExpiresAt > now
                && i.Email != email);

        return new SeatUsage(activeMembers + pendingInvites, limit);
    }

    public async Task<bool> CanAddSeatAsync(Guid workspaceId, string? ignoringPendingEmail = null)
    {
        var usage = await GetSeatUsageAsync(workspaceId, ignoringPendingEmail);
        return usage.Limit is null || usage.Used < usage.Limit;
    }
}
