namespace Renderset.Infrastructure.Persistence.Entities;

public sealed class ReportThemeEntity
{
    public string TenantId { get; set; } = default!;

    public string ThemeId { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>
    /// ReportTheme completo en JSON, igual que ThemeJson en los presets:
    /// añadir una propiedad al tema no exige migración.
    /// </summary>
    public string ThemeJson { get; set; } = default!;

    public bool IsDefault { get; set; }

    public bool Active { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
