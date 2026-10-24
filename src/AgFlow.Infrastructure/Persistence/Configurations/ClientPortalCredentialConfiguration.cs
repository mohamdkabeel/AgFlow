using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Clients.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class ClientPortalCredentialConfiguration : IEntityTypeConfiguration<ClientPortalCredential>
{
    public void Configure(EntityTypeBuilder<ClientPortalCredential> builder)
    {
        builder.ToTable("ClientPortalCredentials");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.PasswordHash).HasMaxLength(256);
        builder.Property(c => c.ActivationTokenHash).HasMaxLength(64);

        // الإيميل = هوية الدخول: واحد لكل Workspace (قاعدة بزنس اتفرضت في الـ Handler، وده الضمان الفعلي)
        builder.HasIndex(c => new { c.WorkspaceId, c.Email }).IsUnique();
        // Contact واحد = Credential واحد
        builder.HasIndex(c => c.ClientContactId).IsUnique();
        // التفعيل بيدور بالـ Hash من غير WorkspaceId
        builder.HasIndex(c => c.ActivationTokenHash).IsUnique().HasFilter("[ActivationTokenHash] IS NOT NULL");
    }
}
