using Renderset.Core.Definitions;

public sealed class ReportDefinition
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; } = 1;

    public ReportHeaderDefinition? Header { get; set; }

    public List<ReportSectionDefinition> Sections { get; set; } = [];

    public ReportFooterDefinition? Footer { get; set; }
}