namespace Renderset.Core.Definitions;

public sealed class ReportHeaderDefinition
{
    public bool VisibleByDefault { get; set; } = true;

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public bool AllowLogo { get; set; } = true;

    public List<ReportFieldDefinition> Fields { get; set; } = [];
}