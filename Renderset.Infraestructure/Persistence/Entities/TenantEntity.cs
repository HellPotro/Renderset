using Renderset.Infrastructure.Persistence.Entities;

public sealed class TenantEntity
{
    public string TenantId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Marca para las páginas que ve el cliente del tenant (visor de
    /// documentos compartidos). Opcional: sin ella se usan el nombre y los
    /// colores por defecto de RenderSet.
    /// </summary>
    public string? LogoUrl { get; set; }

    public string? PrimaryColor { get; set; }

    public string? SecondaryColor { get; set; }

    public ICollection<ReportEntity> Reports { get; set; } = [];

    public ICollection<ReportPresetEntity> Presets { get; set; } = [];

    public ICollection<ReportPresetAssignmentEntity> Assignments { get; set; } = [];

    public ICollection<ReportBlockEntity> ReportBlocks { get; set; } = [];

    public ICollection<ReportVariableEntity> ReportVariables { get; set; } = [];

    public ICollection<ReportResourceEntity> ReportResources { get; set; } = [];

    public ICollection<TenantCultureEntity> Cultures { get; set; } = [];
}