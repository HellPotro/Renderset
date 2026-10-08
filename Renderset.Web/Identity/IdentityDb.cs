using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Renderset.Core.Tenancy;

namespace Renderset.Web.Identity;

/// <summary>
/// Usuarios de Web y su pertenencia a tenants, en la misma base de datos
/// que la API (script Sql/2026-10-06_Identity.sql). Sólo Web lo usa: a la
/// API el usuario le llega dentro del token firmado.
///
/// IdentityUserContext y no IdentityDbContext: no hay roles globales, el rol
/// va por tenant en TenantMembers.
/// </summary>
public sealed class IdentityDb(
    DbContextOptions<IdentityDb> options)
    : IdentityUserContext<RendersetUser>(options)
{
    public DbSet<TenantMemberRow> TenantMembers => Set<TenantMemberRow>();

    public DbSet<TenantInvitationRow> TenantInvitations => Set<TenantInvitationRow>();

    /// <summary>Sólo lectura: la tabla es de la API.</summary>
    public DbSet<TenantRow> Tenants => Set<TenantRow>();

    protected override void OnModelCreating(
        ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RendersetUser>(user =>
        {
            user.Property(x => x.DisplayName).HasMaxLength(100);
            user.Property(x => x.LastTenantId).HasMaxLength(100);
        });

        builder.Entity<TenantMemberRow>(member =>
        {
            member.ToTable("TenantMembers");
            member.HasKey(x => new { x.TenantId, x.UserId });
            member.Property(x => x.TenantId).HasMaxLength(100);
            member.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);

            member.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            member.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId);
        });

        builder.Entity<TenantInvitationRow>(invitation =>
        {
            invitation.ToTable("TenantInvitations");
            invitation.HasKey(x => x.InvitationId);
            invitation.Property(x => x.TenantId).HasMaxLength(100);
            invitation.Property(x => x.Email).HasMaxLength(256);
            invitation.Property(x => x.NormalizedEmail).HasMaxLength(256);
            invitation.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            invitation.Property(x => x.TokenHash).HasMaxLength(32).IsFixedLength();
            invitation.HasIndex(x => x.TokenHash).IsUnique();

            invitation.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId);
        });

        builder.Entity<TenantRow>(tenant =>
        {
            tenant.ToTable("Tenants", table => table.ExcludeFromMigrations());
            tenant.HasKey(x => x.TenantId);
            tenant.Property(x => x.TenantId).HasMaxLength(100);
            tenant.Property(x => x.Name).HasMaxLength(200);
        });
    }
}


public sealed class TenantMemberRow
{
    public string TenantId { get; set; } = default!;

    public string UserId { get; set; } = default!;

    public TenantRole Role { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public RendersetUser User { get; set; } = default!;

    public TenantRow Tenant { get; set; } = default!;
}


public sealed class TenantInvitationRow
{
    public Guid InvitationId { get; set; }

    public string TenantId { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string NormalizedEmail { get; set; } = default!;

    public TenantRole Role { get; set; }

    public byte[] TokenHash { get; set; } = default!;

    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? AcceptedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public TenantRow Tenant { get; set; } = default!;
}


public sealed class TenantRow
{
    public string TenantId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public bool Active { get; set; }
}
