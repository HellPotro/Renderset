namespace Renderset.Core.Definitions;

public sealed class ReportFooterDefinition
{
    public bool VisibleByDefault { get; set; } = true;

    public string? Text { get; set; }

    public bool ShowGenerationDate { get; set; }

    public bool AllowPageNumber { get; set; } = true;
}