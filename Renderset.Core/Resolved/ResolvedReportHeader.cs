namespace Renderset.Core.Resolved;

public sealed class ResolvedReportHeader
{
    public bool Visible { get; set; }

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public bool ShowLogo { get; set; }

    public string? LogoUrl { get; set; }

    public List<ResolvedReportField> Fields { get; set; } = [];
}