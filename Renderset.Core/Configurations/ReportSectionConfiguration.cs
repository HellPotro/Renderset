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

    /// <summary>
    /// Columnas de la cuadrícula cuando Layout es Grid (1 a 12).
    /// </summary>
    public int? Columns { get; set; }

    public List<ReportSectionConfiguration> Sections { get; set; } = [];

    public List<ReportFieldConfiguration> Fields { get; set; } = [];

    public ReportTableConfiguration? Table { get; set; }

    public bool? ShowName { get; set; }
}