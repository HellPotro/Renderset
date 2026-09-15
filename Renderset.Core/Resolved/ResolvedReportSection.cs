using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportSection
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Order { get; init; }

    public bool Visible { get; init; }

    public ReportSectionLayout Layout { get; set; }

    /// <summary>
    /// Colección sobre la que repite la sección. Nulo significa que se pinta
    /// una sola vez con los datos del documento.
    /// </summary>
    public string? DataPath { get; init; }

    public List<ResolvedReportField> Fields { get; init; } = [];

    public ResolvedReportTable? Table { get; init; }

    public List<ResolvedReportSection> Sections { get; init; } = [];

    public bool ShowName { get; set; } = true;

    public ResolvedReportField? GetField(string id)
        => Fields.FirstOrDefault(
            x => string.Equals(
                x.Id,
                id,
                StringComparison.OrdinalIgnoreCase));
}
