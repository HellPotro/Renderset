using Renderset.Core.Localization;

namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class ReportResourceEntity
{
    public string TenantId { get; set; } = default!;

    public string Scope { get; set; } = default!;

    public string ResourceKey { get; set; } = default!;

    public string Culture { get; set; } = default!;

    public string? Value { get; set; }

    public ReportResourceSource Source { get; set; }

    public string? Description { get; set; }

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public TenantEntity Tenant { get; set; } = default!;
}
