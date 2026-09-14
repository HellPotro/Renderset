using Renderset.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class ReportPresetAssignmentEntityConfiguration
    : IEntityTypeConfiguration<ReportPresetAssignmentEntity>
{
    public void Configure(
        EntityTypeBuilder<ReportPresetAssignmentEntity> builder)
    {
        builder.ToTable("ReportPresetAssignments");

        builder.HasKey(x => x.AssignmentId);

        builder.Property(x => x.AssignmentId)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.ReportId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ContextType)
            .HasMaxLength(100);

        builder.Property(x => x.ContextKey)
            .HasMaxLength(200);

        builder.Property(x => x.PresetId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Active)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasOne(x => x.Tenant)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Preset)
            .WithMany(x => x.Assignments)
            .HasForeignKey(
                x => new
                {
                    x.TenantId,
                    x.PresetId
                })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.ReportId,
                x.ContextType,
                x.ContextKey
            })
            .IsUnique()
            .HasDatabaseName(
                "UX_ReportPresetAssignments_Context");
    }
}