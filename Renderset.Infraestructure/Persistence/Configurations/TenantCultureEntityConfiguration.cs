using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class TenantCultureEntityConfiguration
    : IEntityTypeConfiguration<TenantCultureEntity>
{
    public void Configure(
        EntityTypeBuilder<TenantCultureEntity> builder)
    {
        builder.ToTable("TenantCultures");

        builder.HasKey(x => new
        {
            x.TenantId,
            x.Culture
        });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.Culture)
            .HasMaxLength(20);

        builder.Property(x => x.DisplayName)
            .HasMaxLength(100);

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Un único idioma por defecto por tenant, garantizado en base de datos
        // y no sólo en la aplicación.
        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("UX_TenantCultures_Default")
            .IsUnique()
            .HasFilter("[IsDefault] = 1");

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Cultures)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
