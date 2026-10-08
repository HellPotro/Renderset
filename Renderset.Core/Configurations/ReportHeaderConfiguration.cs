using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

public sealed class ReportHeaderConfiguration
{
    public string? BlockId { get; set; }

    public bool? Visible { get; set; }

    public string? TitleKey { get; set; }

    public string? SubtitleKey { get; set; }

    /// <summary>
    /// Nulo = se muestra, como antes de existir la propiedad. El texto se
    /// conserva aunque se oculte, para poder volver a mostrarlo.
    /// </summary>
    public bool? ShowTitle { get; set; }

    public bool? ShowSubtitle { get; set; }

    public bool? ShowLogo { get; set; }

    public string? LogoUrl { get; set; }

    /// <summary>
    /// Alto máximo del logo en píxeles. Nulo deja el del tema.
    /// </summary>
    public int? LogoMaxHeight { get; set; }

    public ReportHeaderLayout? Layout { get; set; }

    /// <summary>
    /// Título junto al logo o en banda propia. Nulo = junto al logo, como
    /// antes de existir la propiedad.
    /// </summary>
    public ReportHeaderTitlePlacement? TitlePlacement { get; set; }

    /// <summary>
    /// Fondo de la banda de cabecera. Nulo significa sin fondo, que es como
    /// se comportaba antes de existir esta propiedad.
    /// </summary>
    public string? BackgroundColor { get; set; }

    public string? TextColor { get; set; }

    public bool? ShowDivider { get; set; }

    /// <summary>
    /// Código QR en la cabecera. Nulo = sin QR, como antes de existir.
    /// </summary>
    public bool? ShowQr { get; set; }

    /// <summary>
    /// Qué lleva el QR. Nulo = el enlace del documento ({documentUrl}).
    /// Admite marcadores de datos: https://erp/salidas/{salidaid}.
    /// Ver <see cref="Rendering.ReportQrContent"/>.
    /// </summary>
    public string? QrContent { get; set; }

    /// <summary>
    /// Lado del QR en píxeles. Nulo = <see cref="Resolved.ResolvedReportHeader.DefaultQrSize"/>.
    /// </summary>
    public int? QrSize { get; set; }

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