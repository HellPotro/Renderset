using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class ReportVariableEntityConfiguration
    : IEntityTypeConfiguration<ReportVariableEntity>
{
    public void Configure(
        EntityTypeBuilder<ReportVariableEntity> builder)
    {
        builder.ToTable("ReportVariables");

        builder.HasKey(x => new
        {
            x.TenantId,
            x.VariableKey
        });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.VariableKey)
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        // Por defecto no traducible: una variable existente no debe empezar
        // a buscarse en el diccionario sólo porque se añada la columna.
        builder.Property(x => x.Translatable)
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.ReportVariables)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}