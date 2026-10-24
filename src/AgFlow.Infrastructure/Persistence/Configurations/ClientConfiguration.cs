using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => c.WorkspaceId);

        // بنكشف الـ _contacts الخاصة (private field) لـ EF Core بس، من غير ما نضطر
        // نعمل Public Setter عليها ونكسر الحماية اللي عملناها في الـ Entity
        builder.Metadata.FindNavigation(nameof(Client.Contacts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.Contacts)
            .WithOne()
            .HasForeignKey(cc => cc.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ClientContactConfiguration : IEntityTypeConfiguration<ClientContact>
{
    public void Configure(EntityTypeBuilder<ClientContact> builder)
    {
        builder.ToTable("ClientContacts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.FullName).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(c => new { c.ClientId, c.Email }).IsUnique();
    }
}
