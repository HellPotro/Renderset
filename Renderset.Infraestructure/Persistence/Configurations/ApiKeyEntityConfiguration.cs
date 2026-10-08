using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Renderset.Core.Security;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyEntityConfiguration
    : IEntityTypeConfiguration<ApiKeyEntity>
{
    public void Configure(
        EntityTypeBuilder<ApiKeyEntity> builder)
    {
        builder.ToTable("ApiKeys");

        builder.HasKey(x => x.ApiKeyId);

        builder.Property(x => x.ApiKeyId)
            .ValueGeneratedNever();

        builder.Property(x => x.TenantId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(ApiKeyService.MaxNameLength)
            .IsRequired();

        builder.Property(x => x.Prefix)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.KeyHash)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // Cada petición autenticada busca por hash: índice único.
        builder.HasIndex(x => x.KeyHash)
            .IsUnique()
            .HasDatabaseName("UX_ApiKeys_KeyHash");

        builder.HasIndex(x => x.TenantId)
            .HasDatabaseName("IX_ApiKeys_Tenant");

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
