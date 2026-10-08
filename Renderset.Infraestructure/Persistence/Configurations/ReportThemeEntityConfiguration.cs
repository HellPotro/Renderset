using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class ReportThemeEntityConfiguration
    : IEntityTypeConfiguration<ReportThemeEntity>
{
    public void Configure(
        EntityTypeBuilder<ReportThemeEntity> builder)
    {
        builder.ToTable("ReportThemes");

        builder.HasKey(x =>
            new
            {
                x.TenantId,
                x.ThemeId
            });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.ThemeId)
            .HasMaxLength(64);

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ThemeJson)
            .IsRequired();

        builder.Property(x => x.IsDefault)
            .HasDefaultValue(false);

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.Active
            })
            .HasDatabaseName("IX_ReportThemes_Tenant");
    }
}
