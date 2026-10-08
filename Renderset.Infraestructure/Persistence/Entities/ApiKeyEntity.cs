namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class ApiKeyEntity
{
    public Guid ApiKeyId { get; set; }

    public string TenantId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string Prefix { get; set; } = default!;

    /// <summary>
    /// SHA-256 de la clave. La clave en claro no se guarda.
    /// </summary>
    public byte[] KeyHash { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public TenantEntity Tenant { get; set; } = default!;
}
