using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Deca;

namespace Renderset.Infrastructure.Persistence.Deca;

/// <summary>
/// Tablas de la base de datos Deca. El script de creación está en
/// Sql/2026-10-08_Deca.sql; esto tiene que coincidir con él.
/// </summary>
public sealed class DecaDocumentEntityConfiguration
    : IEntityTypeConfiguration<DecaDocumentEntity>
{
    public void Configure(
        EntityTypeBuilder<DecaDocumentEntity> builder)
    {
        // Con triggers declarados EF no usa OUTPUT sin INTO, que SQL Server
        // no admite en tablas con triggers.
        builder.ToTable("DecaDocuments", t => t.HasTrigger("TR_DecaDocuments_Retention"));

        builder.HasKey(x => x.DecaId);

        // Para la FK compuesta desde las versiones.
        builder.HasAlternateKey(x => new { x.DecaId, x.TenantId });

        builder.Property(x => x.DecaId)
            .HasMaxLength(32)
            .IsUnicode(false)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Number)
            .HasMaxLength(30)
            .IsRequired();

        // Binaria: el código es base64url y distingue mayúsculas.
        builder.Property(x => x.PublicCode)
            .HasMaxLength(DecaPublicCode.Length)
            .IsUnicode(false)
            .UseCollation("Latin1_General_100_BIN2")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Reference).HasMaxLength(200);
        builder.Property(x => x.ShipperName).HasMaxLength(200);
        builder.Property(x => x.ShipperTaxId).HasMaxLength(20);
        builder.Property(x => x.CarrierName).HasMaxLength(200);
        builder.Property(x => x.CarrierTaxId).HasMaxLength(20);
        builder.Property(x => x.TractorPlate).HasMaxLength(20);

        builder.Property(x => x.DataJson)
            .IsRequired();

        builder.Property(x => x.IssuedBy).HasMaxLength(200);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasMany(x => x.Versions)
            .WithOne(x => x.Deca)
            .HasForeignKey(x => new { x.DecaId, x.TenantId })
            .HasPrincipalKey(x => new { x.DecaId, x.TenantId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Events)
            .WithOne(x => x.Deca)
            .HasForeignKey(x => x.DecaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PublicCode)
            .IsUnique()
            .HasDatabaseName("UX_DecaDocuments_PublicCode");

        builder.HasIndex(x => new { x.TenantId, x.Year, x.Sequence })
            .IsUnique()
            .HasDatabaseName("UX_DecaDocuments_Number");

        builder.HasIndex(x => new { x.TenantId, x.IssuedAtUtc })
            .HasDatabaseName("IX_DecaDocuments_Tenant_Issued");
    }
}


public sealed class DecaVersionEntityConfiguration
    : IEntityTypeConfiguration<DecaVersionEntity>
{
    public void Configure(
        EntityTypeBuilder<DecaVersionEntity> builder)
    {
        builder.ToTable("DecaVersions", t => t.HasTrigger("TR_DecaVersions_Immutable"));

        builder.HasKey(x => new { x.DecaId, x.Version });

        builder.Property(x => x.DecaId)
            .HasMaxLength(32)
            .IsUnicode(false);

        builder.Property(x => x.TenantId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.DataJson)
            .IsRequired();

        builder.Property(x => x.Sha256)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.PdfPath)
            .HasMaxLength(400);

        builder.Property(x => x.CreatedBy).HasMaxLength(200);
        builder.Property(x => x.Reason).HasMaxLength(500);
    }
}


public sealed class DecaEventEntityConfiguration
    : IEntityTypeConfiguration<DecaEventEntity>
{
    public void Configure(
        EntityTypeBuilder<DecaEventEntity> builder)
    {
        builder.ToTable("DecaEvents", t => t.HasTrigger("TR_DecaEvents_Immutable"));

        builder.HasKey(x => x.EventId);

        builder.Property(x => x.EventId)
            .UseIdentityColumn();

        builder.Property(x => x.DecaId)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasMaxLength(40)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Actor).HasMaxLength(200);
        builder.Property(x => x.Detail).HasMaxLength(1000);

        builder.HasIndex(x => new { x.DecaId, x.AtUtc })
            .HasDatabaseName("IX_DecaEvents_Deca");
    }
}


public sealed class DecaTemplateEntityConfiguration
    : IEntityTypeConfiguration<DecaTemplateEntity>
{
    public void Configure(
        EntityTypeBuilder<DecaTemplateEntity> builder)
    {
        builder.ToTable("DecaTemplates");

        builder.HasKey(x => new { x.TenantId, x.Version });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);

        builder.Property(x => x.TextsJson)
            .IsRequired();

        builder.Property(x => x.UpdatedBy).HasMaxLength(200);
    }
}


public sealed class DecaSequenceEntityConfiguration
    : IEntityTypeConfiguration<DecaSequenceEntity>
{
    public void Configure(
        EntityTypeBuilder<DecaSequenceEntity> builder)
    {
        builder.ToTable("DecaSequences");

        builder.HasKey(x => new { x.TenantId, x.Year });

        builder.Property(x => x.TenantId)
            .HasMaxLength(100);
    }
}
