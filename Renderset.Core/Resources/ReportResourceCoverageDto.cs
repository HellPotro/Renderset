namespace Renderset.Core.Resources;

public sealed class ReportResourceCoverageDto
{
    public required string Scope { get; set; }

    public required string Culture { get; set; }

    public int Total { get; set; }

    public int Translated { get; set; }

    public int Missing { get; set; }

    public decimal Percentage { get; set; }
}