namespace Renderset.Core.Configurations;

public sealed class ReportFooterConfiguration
{
    public string? BlockId { get; set; }

    public bool? Visible { get; set; }

    public string? TextKey { get; set; }

    public bool? ShowGenerationDate { get; set; }

    public bool? ShowPageNumber { get; set; }
}