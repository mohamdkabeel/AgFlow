using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(300);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(t => t.WorkspaceId);
        builder.HasIndex(t => t.TaskListId);

        builder.Metadata.FindNavigation(nameof(TaskItem.Assignments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(t => t.Assignments)
            .WithOne()
            .HasForeignKey(a => a.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TaskListConfiguration : IEntityTypeConfiguration<TaskList>
{
    public void Configure(EntityTypeBuilder<TaskList> builder)
    {
        builder.ToTable("TaskLists");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => t.WorkspaceId);
        builder.HasIndex(t => t.ProjectId);
    }
}

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("TaskAssignments");
        builder.HasKey(a => a.Id);

        // مينفعش نفس الشخص يتكرر على نفس الـ Task مرتين على مستوى الـ Database نفسه
        // (طبقة حماية إضافية فوق الـ Check اللي عملناه في TaskItem.AssignTo)
        builder.HasIndex(a => new { a.TaskItemId, a.UserId }).IsUnique();
    }
}