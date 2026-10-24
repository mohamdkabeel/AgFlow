using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.TimeTracking.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("TimeEntries");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.DurationHours).HasColumnType("decimal(8,2)");
        builder.Property(t => t.HourlyCostAtTimeOfEntry).HasColumnType("decimal(10,2)");

        // Composite Index (Phase 5, Step 4) — تقارير الـ Timesheet بتفلتر بالاتنين مع بعض
        builder.HasIndex(t => new { t.TaskItemId, t.UserId });
        // محتاجينه لحساب الـ Retainer Reconciliation الشهري بسرعة
        builder.HasIndex(t => new { t.WorkspaceId, t.WorkDate });
    }
}
