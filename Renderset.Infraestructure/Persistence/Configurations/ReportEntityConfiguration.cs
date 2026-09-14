using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class ReportEntityConfiguration
    : IEntityTypeConfiguration<ReportEntity>
{
    public void Configure(
        EntityTypeBuilder<ReportEntity> builder)
    {
        builder.ToTable("Reports");

        builder.HasKey(x =>
            new
            {
                x.TenantId,
                x.ReportId
            });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.ReportId)
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasDefaultValue(1);

        builder.Property(x => x.DefinitionJson)
            .IsRequired();

        builder.Property(x => x.SchemaJson)
            .IsRequired();

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Property(x => x.SampleDataJson);

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Reports)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.Active
            })
            .HasDatabaseName(
                "IX_Reports_Tenant_Active");
    }
}