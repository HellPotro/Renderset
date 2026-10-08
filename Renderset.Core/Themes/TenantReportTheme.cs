namespace Renderset.Core.Themes;

/// <summary>
/// Tema de empresa guardado en el tenant.
/// </summary>
public sealed class TenantReportTheme
{
    public required ReportTheme Theme { get; set; }

    /// <summary>
    /// Tema con el que nacen los presets nuevos. Como mucho uno por tenant.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Presets que lo usan. Lo calcula la API al listar; no se guarda.
    /// </summary>
    public int PresetCount { get; set; }
}
