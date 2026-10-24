using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Projects.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        // RetainerAgreementId اختياري — مفيش .IsRequired() هنا، وده بيعكس
        // الـ Nullable Guid? في الـ Entity نفسها
        builder.HasIndex(p => p.WorkspaceId);
        builder.HasIndex(p => p.ClientId);
    }
}
