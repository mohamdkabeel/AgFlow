using AgFlow.Domain.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgFlow.Infrastructure.Persistence.Configurations
{
    public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
    {
        public void Configure(EntityTypeBuilder<Workspace> builder)
        {
            builder.ToTable("Workspaces");
            builder.HasKey(w => w.Id);
            builder.Property(w => w.Name)
          .IsRequired()
          .HasMaxLength(200);

            builder.Property(w => w.Subdomain)
                .IsRequired()
                .HasMaxLength(100);

            // Index لأن هنبحث بيه كتير (تسجيل الدخول عبر subdomain مثلاً)
            builder.HasIndex(w => w.Subdomain).IsUnique();

        }
    }
}
