namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class TenantCultureEntity
{
    public string TenantId { get; set; } = default!;

    public string Culture { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public bool IsDefault { get; set; }

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public TenantEntity Tenant { get; set; } = default!;
}
