using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class TenantAssetEntityConfiguration
    : IEntityTypeConfiguration<TenantAssetEntity>
{
    public void Configure(
        EntityTypeBuilder<TenantAssetEntity> builder)
    {
        builder.ToTable("TenantAssets");

        builder.HasKey(x => new
        {
            x.TenantId,
            x.AssetId
        });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.AssetId)
            .HasMaxLength(32)
            .IsUnicode(false);

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(x => x.FileName)
            .HasMaxLength(260);

        builder.Property(x => x.Path)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }
}
