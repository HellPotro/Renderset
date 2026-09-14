namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class ReportEntity
{
    public string TenantId { get; set; } = default!;

    public string ReportId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int Version { get; set; }

    public string DefinitionJson { get; set; } = default!;

    public string SchemaJson { get; set; } = default!;

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public string? SampleDataJson { get; set; }

    public TenantEntity Tenant { get; set; } = default!;
}