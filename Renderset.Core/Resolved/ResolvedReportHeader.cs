using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportHeader
{
    public bool Visible { get; set; }

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public bool ShowTitle { get; set; } = true;

    public bool ShowSubtitle { get; set; } = true;

    public bool ShowLogo { get; set; }

    public string? LogoUrl { get; set; }

    public int? LogoMaxHeight { get; set; }

    public ReportHeaderLayout Layout { get; set; } =
        ReportHeaderLayout.LogoLeft;

    public ReportHeaderTitlePlacement TitlePlacement { get; set; } =
        ReportHeaderTitlePlacement.Inline;

    public string? BackgroundColor { get; set; }

    public string? TextColor { get; set; }

    public bool ShowDivider { get; set; } = true;

    public const int DefaultQrSize = 84;
    public const int MinQrSize = 56;
    public const int MaxQrSize = 160;

    public bool ShowQr { get; set; }

    /// <summary>
    /// Plantilla del QR, sin resolver: los datos y el enlace del documento
    /// sólo se conocen al pintar.
    /// </summary>
    public string QrContent { get; set; } =
        Rendering.ReportQrContent.DocumentUrlTemplate;

    public int QrSize { get; set; } = DefaultQrSize;

    public List<ResolvedReportField> Fields { get; set; } = [];

    /// <summary>
    /// Líneas de datos ya traducidas y en orden. Las vacías no llegan aquí.
    /// </summary>
    public List<ResolvedReportHeaderLine> Lines { get; set; } = [];

    /// <summary>
    /// Si hay columnas, el renderer usa este diseño avanzado. Si está vacío,
    /// conserva el camino legacy basado en Lines.
    /// </summary>
    public List<ResolvedReportHeaderColumn> Columns { get; set; } = [];
}