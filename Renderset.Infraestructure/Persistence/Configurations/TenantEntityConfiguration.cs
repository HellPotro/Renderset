using Renderset.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class TenantEntityConfiguration
    : IEntityTypeConfiguration<TenantEntity>
{
    public void Configure(
        EntityTypeBuilder<TenantEntity> builder)
    {
        builder.ToTable("Tenants");

        builder.HasKey(x => x.TenantId);

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");
    }
}