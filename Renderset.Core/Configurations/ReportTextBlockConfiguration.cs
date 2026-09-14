namespace Renderset.Core.Configurations;

public sealed class ReportTextBlockConfiguration
{
    public string? Title { get; set; }

    public string Text { get; set; } = string.Empty;

    public ReportTextBlockStyle Style { get; set; } = ReportTextBlockStyle.Default;
}

public enum ReportTextBlockStyle
{
    Default,
    Information,
    Warning,
    Success
}
