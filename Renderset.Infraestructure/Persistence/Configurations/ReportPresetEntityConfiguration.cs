using Renderset.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class ReportPresetEntityConfiguration
    : IEntityTypeConfiguration<ReportPresetEntity>
{
    public void Configure(
        EntityTypeBuilder<ReportPresetEntity> builder)
    {
        builder.ToTable("ReportPresets");

        builder.HasKey(x =>
            new
            {
                x.TenantId,
                x.PresetId
            });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.PresetId)
            .HasMaxLength(100);

        builder.Property(x => x.ReportId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasDefaultValue(1);

        builder.Property(x => x.ConfigurationJson)
            .IsRequired();

        builder.Property(x => x.ThemeJson)
            .IsRequired();

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Presets)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.ReportId,
                x.Active
            })
            .HasDatabaseName(
                "IX_ReportPresets_Report");
    }
}