using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Core.Sharing;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class DocumentBundleEntityConfiguration
    : IEntityTypeConfiguration<DocumentBundleEntity>
{
    public void Configure(
        EntityTypeBuilder<DocumentBundleEntity> builder)
    {
        builder.ToTable("DocumentBundles");

        // Clave sólo por Guid, sin tenant: el enlace público llega sin tenant
        // y tiene que poder resolverse con una búsqueda por clave.
        builder.HasKey(x => x.BundleId);

        builder.Property(x => x.BundleId)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasMaxLength(DocumentBundleService.MaxTitleLength)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasMaxLength(DocumentBundleService.MaxMessageLength);

        builder.Property(x => x.Culture)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(BundleToken.HashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.ProtectedToken)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.AccessCount)
            .HasDefaultValue(0);

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Bundle)
            .HasForeignKey(x => x.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.CreatedAtUtc
            })
            .HasDatabaseName("IX_DocumentBundles_Tenant");
    }
}


public sealed class DocumentBundleItemEntityConfiguration
    : IEntityTypeConfiguration<DocumentBundleItemEntity>
{
    public void Configure(
        EntityTypeBuilder<DocumentBundleItemEntity> builder)
    {
        builder.ToTable("DocumentBundleItems");

        builder.HasKey(x =>
            new
            {
                x.BundleId,
                x.Position
            });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DocumentId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(DocumentBundleService.MaxDisplayNameLength)
            .IsRequired();

        builder.HasOne<RenderedDocumentEntity>()
            .WithMany()
            .HasForeignKey(x =>
                new
                {
                    x.TenantId,
                    x.DocumentId
                })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.DocumentId
            })
            .HasDatabaseName("IX_DocumentBundleItems_Document");
    }
}


public sealed class DocumentBundleAccessEntityConfiguration
    : IEntityTypeConfiguration<DocumentBundleAccessEntity>
{
    public void Configure(
        EntityTypeBuilder<DocumentBundleAccessEntity> builder)
    {
        builder.ToTable("DocumentBundleAccesses");

        builder.HasKey(x => x.AccessId);

        builder.Property(x => x.AccessId)
            .UseIdentityColumn();

        builder.Property(x => x.TenantId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasMaxLength(64);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(512);

        builder.Property(x => x.AccessedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne<DocumentBundleEntity>()
            .WithMany()
            .HasForeignKey(x => x.BundleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x =>
            new
            {
                x.BundleId,
                x.AccessedAtUtc
            })
            .HasDatabaseName("IX_DocumentBundleAccesses_Bundle");
    }
}
