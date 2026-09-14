using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

public sealed class ReportSectionConfiguration
{
    public string SectionId { get; set; } = default!;

    public string? SourceSectionId { get; set; }

    public string? NameKey { get; set; }

    public bool? Visible { get; set; }

    public int? Order { get; set; }

    public ReportSectionLayout? Layout { get; set; }

    public List<ReportFieldConfiguration> Fields { get; set; } = [];

    public ReportTableConfiguration? Table { get; set; }
}