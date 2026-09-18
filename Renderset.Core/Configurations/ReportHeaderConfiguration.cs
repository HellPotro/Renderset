using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

public sealed class ReportHeaderConfiguration
{
    public string? BlockId { get; set; }

    public bool? Visible { get; set; }

    public string? TitleKey { get; set; }

    public string? SubtitleKey { get; set; }

    public bool? ShowLogo { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>
    /// Alto máximo del logo en píxeles. Nulo deja el del tema.
    /// </summary>
    public int? LogoMaxHeight { get; set; }

    public ReportHeaderLayout? Layout { get; set; }

    /// <summary>
    /// Fondo de la banda de cabecera. Nulo significa sin fondo, que es como
    /// se comportaba antes de existir esta propiedad.
    /// </summary>
    public string? BackgroundColor { get; set; }

    public string? TextColor { get; set; }

    public bool? ShowDivider { get; set; }

    /// <summary>
    /// Líneas de datos de empresa. Sparse como el resto: si el preset no
    /// define ninguna, se quedan las del bloque.
    /// </summary>
    public List<ReportHeaderLineConfiguration> Lines { get; set; } = [];

    /// <summary>
    /// Diseño avanzado de la cabecera. Mientras esté vacío se conserva el
    /// render histórico de Title / Subtitle / Lines exactamente igual.
    /// </summary>
    public List<ReportHeaderColumnConfiguration> Columns { get; set; } = [];
}