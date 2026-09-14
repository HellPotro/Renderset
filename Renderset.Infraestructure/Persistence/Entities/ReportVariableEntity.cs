using Renderset.Core.Variables;

namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class ReportVariableEntity
{
    public string TenantId { get; set; } = default!;

    public string VariableKey { get; set; } = default!;

    public string? Value { get; set; }

    public string? Description { get; set; }

    public ReportVariableType Type { get; set; }

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public TenantEntity Tenant { get; set; } = default!;
}