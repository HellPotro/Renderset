using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

public sealed class ReportConfiguration
{
    public required string Id { get; set; }

    public required string ReportId { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; }

    public ReportHeaderConfiguration? Header { get; set; }

    public List<ReportSectionConfiguration> Sections { get; set; } = [];

    public List<ReportBodyItemConfiguration> Body { get; set; } = [];

    public List<ReportFieldPlacementConfiguration> FieldPlacements { get; set; } = [];

    public ReportFooterConfiguration? Footer { get; set; }
}