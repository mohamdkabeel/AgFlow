using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class RetainerAgreementConfiguration : IEntityTypeConfiguration<RetainerAgreement>
{
    public void Configure(EntityTypeBuilder<RetainerAgreement> builder)
    {
        builder.ToTable("RetainerAgreements");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.MonthlyPrice).HasColumnType("decimal(12,2)");

        builder.HasIndex(r => r.WorkspaceId);

        // قاعدة: Retainer نشط واحد بس لكل عميل. الفحص في الـ Handler للرسالة الواضحة،
        // والـ Index هو الضمان الفعلي لو طلبين اتبعتوا في نفس اللحظة
        builder.HasIndex(r => new { r.WorkspaceId, r.ClientId })
            .IsUnique()
            .HasFilter("[IsActive] = 1");
    }
}
