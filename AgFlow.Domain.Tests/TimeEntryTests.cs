using AgFlow.Domain.Common;
using AgFlow.Domain.Modules.TimeTracking.Entities;
using Xunit;

namespace AgFlow.Domain.Tests;

public class TimeEntryTests
{
    private static TimeEntry Log(decimal hours = 2m, decimal cost = 30m, bool requiresApproval = false) =>
        TimeEntry.Log(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateOnly.FromDateTime(DateTime.UtcNow), hours, cost, isBillable: true, note: null, requiresApproval);

    [Fact]
    public void Log_SnapshotsHourlyCost()
    {
        var entry = Log(cost: 45m);
        Assert.Equal(45m, entry.HourlyCostAtTimeOfEntry);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Log_WithNonPositiveDuration_Throws(double hours)
    {
        Assert.Throws<ArgumentException>(() => Log(hours: (decimal)hours));
    }

    [Fact]
    public void Log_WhenApprovalNotRequired_IsApprovedAutomatically_WithoutApprover()
    {
        var entry = Log(requiresApproval: false);

        Assert.True(entry.IsApproved);
        Assert.Null(entry.ApprovedByUserId); // null = اعتماد تلقائي
    }

    [Fact]
    public void Log_WhenApprovalRequired_StartsPending()
    {
        var entry = Log(requiresApproval: true);

        Assert.False(entry.IsApproved);
        Assert.Null(entry.ApprovedAt);
    }

    [Fact]
    public void Approve_RecordsWhoAndWhen()
    {
        var entry = Log(requiresApproval: true);
        var approver = Guid.NewGuid();

        entry.Approve(approver);

        Assert.True(entry.IsApproved);
        Assert.Equal(approver, entry.ApprovedByUserId);
        Assert.NotNull(entry.ApprovedAt);
    }

    [Fact]
    public void Approve_Twice_KeepsFirstApprover()
    {
        var entry = Log(requiresApproval: true);
        var first = Guid.NewGuid();

        entry.Approve(first);
        entry.Approve(Guid.NewGuid());

        Assert.Equal(first, entry.ApprovedByUserId);
    }

    [Fact]
    public void UpdateDuration_AfterApproval_Throws()
    {
        var entry = Log(requiresApproval: false); // معتمد تلقائياً
        Assert.Throws<DomainException>(() => entry.UpdateDuration(5m));
    }

    [Fact]
    public void UpdateDuration_WhilePending_Works()
    {
        var entry = Log(requiresApproval: true);
        entry.UpdateDuration(5m);
        Assert.Equal(5m, entry.DurationHours);
    }
}
