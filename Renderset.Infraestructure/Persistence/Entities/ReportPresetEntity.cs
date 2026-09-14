namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class ReportPresetEntity
{
    public string TenantId { get; set; } = default!;

    public string PresetId { get; set; } = default!;

    public string ReportId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int Version { get; set; }

    public string ConfigurationJson { get; set; } = default!;

    public string ThemeJson { get; set; } = default!;

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public TenantEntity Tenant { get; set; } = default!;

    public ICollection<ReportPresetAssignmentEntity> Assignments { get; set; } = [];
}