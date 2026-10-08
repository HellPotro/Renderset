namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class DataMappingEntity
{
    public string TenantId { get; set; } = default!;

    public string MappingId { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>
    /// Report que lo usa por defecto cuando llegan filas sin mappingId.
    /// </summary>
    public string? ReportId { get; set; }

    /// <summary>
    /// Árbol del mapping (DataMappingNode) en JSON.
    /// </summary>
    public string MappingJson { get; set; } = default!;

    public int Version { get; set; }

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
