using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AgFlow.Domain.Modules.Files.Entities;

namespace AgFlow.Infrastructure.Persistence.Configurations;

public class FileAssetConfiguration : IEntityTypeConfiguration<FileAsset>
{
    public void Configure(EntityTypeBuilder<FileAsset> builder)
    {
        builder.ToTable("FileAssets");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName).IsRequired().HasMaxLength(260);
        builder.Property(f => f.ApprovalStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.ReviewNote).HasMaxLength(FileAsset.MaxReviewNoteLength);

        builder.Ignore(f => f.LatestVersionNumber); // محسوبة، مش عمود

        builder.HasMany(f => f.Versions)
               .WithOne()
               .HasForeignKey(v => v.FileAssetId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(f => f.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(f => new { f.WorkspaceId, f.TaskItemId });
        builder.HasIndex(f => new { f.WorkspaceId, f.ApprovalStatus });
    }
}

public class FileVersionConfiguration : IEntityTypeConfiguration<FileVersion>
{
    public void Configure(EntityTypeBuilder<FileVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.BlobPath).IsRequired().HasMaxLength(1024);

        // رقم النسخة فريد جوه الملف: يمنع نسختين بنفس الرقم لو اتنين رفعوا في نفس اللحظة
        builder.HasIndex(v => new { v.FileAssetId, v.VersionNumber }).IsUnique();
    }
}
