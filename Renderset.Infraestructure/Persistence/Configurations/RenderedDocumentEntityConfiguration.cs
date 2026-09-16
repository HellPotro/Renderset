using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class RenderedDocumentEntityConfiguration
    : IEntityTypeConfiguration<RenderedDocumentEntity>
{
    public void Configure(
        EntityTypeBuilder<RenderedDocumentEntity> builder)
    {
        builder.ToTable("RenderedDocuments");

        builder.HasKey(x =>
            new
            {
                x.TenantId,
                x.DocumentId
            });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.DocumentId)
            .HasMaxLength(64);

        builder.Property(x => x.ReportId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.PresetId)
            .HasMaxLength(100);

        builder.Property(x => x.Culture)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Format)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Content)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.ReportId,
                x.CreatedAtUtc
            })
            .HasDatabaseName(
                "IX_RenderedDocuments_Report");
    }
}
