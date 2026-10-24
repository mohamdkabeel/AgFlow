using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Subscriptions.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.PlanName).IsRequired().HasMaxLength(50);
        builder.Property(s => s.StripeCustomerId).HasMaxLength(255);
        builder.Property(s => s.StripeSubscriptionId).HasMaxLength(255);

        // اشتراك واحد لكل Workspace. الـ Middleware والـ Queries بتفترض ده
        builder.HasIndex(s => s.WorkspaceId).IsUnique();

        // الـ Webhook بيدور بالـ StripeSubscriptionId (من غير WorkspaceId)
        builder.HasIndex(s => s.StripeSubscriptionId)
            .IsUnique()
            .HasFilter("[StripeSubscriptionId] IS NOT NULL");
    }
}
