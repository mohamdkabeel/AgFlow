using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Identity.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Email).IsRequired().HasMaxLength(256);
        builder.Property(i => i.TokenHash).IsRequired().HasMaxLength(64); // SHA-256 hex
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.HourlyCost).HasColumnType("decimal(10,2)");

        // قبول الدعوة بيدور بالـ Hash (من غير WorkspaceId)، فلازم Unique Index سريع
        builder.HasIndex(i => i.TokenHash).IsUnique();

        // دعوة Pending واحدة بس لكل إيميل في الـ Workspace. إعادة الدعوة بتلغي القديمة الأول
        builder.HasIndex(i => new { i.WorkspaceId, i.Email })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");
    }
}

public class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("WorkspaceMembers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.HourlyCost).HasColumnType("decimal(10,2)");

        // مستخدم واحد = عضوية واحدة في الـ Workspace. من غيره قبول دعوتين متزامنتين كان ممكن يعمل عضوية مكررة
        builder.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId); // Login بيدور بالـ UserId من غير WorkspaceId
    }
}
