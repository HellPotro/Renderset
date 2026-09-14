using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class ReportResourceEntityConfiguration
    : IEntityTypeConfiguration<ReportResourceEntity>
{
    public void Configure(
        EntityTypeBuilder<ReportResourceEntity> builder)
    {
        builder.ToTable("ReportResources");

        builder.HasKey(x => new
        {
            x.TenantId,
            x.Scope,
            x.ResourceKey,
            x.Culture
        });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.Scope)
            .HasMaxLength(100);

        builder.Property(x => x.ResourceKey)
            .HasMaxLength(200);

        builder.Property(x => x.Culture)
            .HasMaxLength(20);

        builder.Property(x => x.Value)
            .HasMaxLength(1000);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Source)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new
            {
                x.TenantId,
                x.Culture,
                x.Scope
            })
            .HasDatabaseName("IX_ReportResources_Lookup")
            .IncludeProperties(x => new
            {
                x.ResourceKey,
                x.Value
            })
            .HasFilter("[Active] = 1");

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.ReportResources)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
