using Renderset.Infrastructure.Persistence.Entities;
using Renderset.Infrastructure.Persistence.Entities;

public sealed class TenantEntity
{
    public string TenantId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<ReportEntity> Reports { get; set; } = [];

    public ICollection<ReportPresetEntity> Presets { get; set; } = [];

    public ICollection<ReportPresetAssignmentEntity> Assignments { get; set; } = [];

    public ICollection<ReportBlockEntity> ReportBlocks { get; set; } = [];

    public ICollection<ReportVariableEntity> ReportVariables { get; set; } = [];
}