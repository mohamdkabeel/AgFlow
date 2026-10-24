using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Billing.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ClientNameSnapshot).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(i => i.WorkspaceId);
        builder.HasIndex(i => new { i.ClientId, i.Status }); // Phase 5, Step 4

        // === خط الدفاع الأخير ضد الفواتير المكررة ===
        // الفحص في الكود لوحده مش كفاية (طلبين متزامنين يعدّوا الفحص مع بعض).
        // الـ Index هو اللي بيضمن فعلاً: فاتورة واحدة (مش Void) لكل Retainer لكل فترة.
        // Void مستثناة عشان لو فاتورة اتلغت نقدر نولّد بديلها.
        builder.HasIndex(i => new { i.WorkspaceId, i.RetainerAgreementId, i.PeriodStart })
            .IsUnique()
            .HasFilter("[RetainerAgreementId] IS NOT NULL AND [Status] <> 'Void'");

        // لازم نقول لـ EF صراحةً يتجاهل الـ Events، وإلا ممكن يحاول يعاملها كـ Entity ويفشل وقت بناء الـ Model
        builder.Ignore(i => i.DomainEvents);

        builder.Metadata.FindNavigation(nameof(Invoice.LineItems))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(i => i.LineItems)
            .WithOne()
            .HasForeignKey(li => li.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.ToTable("InvoiceLineItems");
        builder.HasKey(li => li.Id);
        builder.Property(li => li.Description).IsRequired().HasMaxLength(500);
        // بدون تحديد دقة، EF بيستخدم decimal(18,2) مع تحذير — في الفلوس بنحددها صراحة
        builder.Property(li => li.Amount).HasColumnType("decimal(12,2)");
    }
}

public class OverageApprovalConfiguration : IEntityTypeConfiguration<OverageApproval>
{
    public void Configure(EntityTypeBuilder<OverageApproval> builder)
    {
        builder.ToTable("OverageApprovals");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OverageHours).HasColumnType("decimal(8,2)");
        builder.Property(o => o.OverageAmount).HasColumnType("decimal(12,2)");
        builder.Property(o => o.Decision).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(o => o.WorkspaceId);
        builder.HasIndex(o => o.InvoiceId);
    }
}
