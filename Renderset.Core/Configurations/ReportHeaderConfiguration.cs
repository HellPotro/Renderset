namespace Renderset.Core.Configurations;

public sealed class ReportHeaderConfiguration
{
    public string? BlockId { get; set; }

    public bool? Visible { get; set; }

    public string? TitleKey { get; set; }

    public string? SubtitleKey { get; set; }

    public bool? ShowLogo { get; set; }

    public string? LogoUrl { get; set; }
}