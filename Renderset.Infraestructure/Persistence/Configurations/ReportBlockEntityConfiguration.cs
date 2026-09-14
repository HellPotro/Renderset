using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class ReportBlockEntityConfiguration
    : IEntityTypeConfiguration<ReportBlockEntity>
{
    public void Configure(
        EntityTypeBuilder<ReportBlockEntity> builder)
    {
        builder.ToTable("ReportBlocks");

        builder.HasKey(x => new
        {
            x.TenantId,
            x.BlockId
        });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.BlockId)
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ConfigurationJson)
            .IsRequired();

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.ReportBlocks)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}