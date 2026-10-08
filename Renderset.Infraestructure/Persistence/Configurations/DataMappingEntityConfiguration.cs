using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class DataMappingEntityConfiguration
    : IEntityTypeConfiguration<DataMappingEntity>
{
    public void Configure(
        EntityTypeBuilder<DataMappingEntity> builder)
    {
        builder.ToTable("DataMappings");

        builder.HasKey(x =>
            new
            {
                x.TenantId,
                x.MappingId
            });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.MappingId)
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.ReportId)
            .HasMaxLength(100);

        builder.Property(x => x.MappingJson)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasDefaultValue(1);

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.ReportId,
                x.Active
            })
            .HasDatabaseName("IX_DataMappings_Report");
    }
}
