using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportHeader
{
    public bool Visible { get; set; }

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public bool ShowLogo { get; set; }

    public string? LogoUrl { get; set; }

    public int? LogoMaxHeight { get; set; }

    public ReportHeaderLayout Layout { get; set; } =
        ReportHeaderLayout.LogoLeft;

    public string? BackgroundColor { get; set; }

    public string? TextColor { get; set; }

    public bool ShowDivider { get; set; } = true;

    public List<ResolvedReportField> Fields { get; set; } = [];

    /// <summary>
    /// Líneas de datos ya traducidas y en orden. Las vacías no llegan aquí.
    /// </summary>
    public List<ResolvedReportHeaderLine> Lines { get; set; } = [];
}