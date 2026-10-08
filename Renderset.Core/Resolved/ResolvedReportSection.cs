using Renderset.Core.Definitions;

namespace Renderset.Core.Resolved;

public sealed class ResolvedReportSection
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Order { get; init; }

    public bool Visible { get; init; }

    /// <summary>
    /// Ya normalizado: List o Grid. Los Columns2/3/4 de presets antiguos
    /// llegan aquí como Grid con su número de columnas.
    /// </summary>
    public ReportSectionLayout Layout { get; set; }

    /// <summary>
    /// Columnas de la cuadrícula. 1 en lista.
    /// </summary>
    public int Columns { get; set; } = 1;

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
