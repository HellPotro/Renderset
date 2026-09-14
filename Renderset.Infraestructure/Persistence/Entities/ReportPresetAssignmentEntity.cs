namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class ReportPresetAssignmentEntity
{
    public long AssignmentId { get; set; }

    public string TenantId { get; set; } = default!;

    public string ReportId { get; set; } = default!;

    public string? ContextType { get; set; }

    public string? ContextKey { get; set; }

    public string PresetId { get; set; } = default!;

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = default!;

    public TenantEntity Tenant { get; set; } = default!;

    public ReportPresetEntity Preset { get; set; } = default!;
}