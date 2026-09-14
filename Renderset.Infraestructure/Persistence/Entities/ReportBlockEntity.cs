using Renderset.Core.Blocks;

namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class ReportBlockEntity
{
    public string TenantId { get; set; } = default!;

    public string BlockId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public ReportBlockType Type { get; set; }

    public string ConfigurationJson { get; set; } = "{}";

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public TenantEntity Tenant { get; set; } = default!;
}