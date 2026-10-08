using Renderset.Core.Definitions;

namespace Renderset.Core.Configurations;

public sealed class ReportConfiguration
{
    public required string Id { get; set; }

    public required string ReportId { get; set; }

    public required string Name { get; set; }

    public int Version { get; set; }

    /// <summary>
    /// Tema de empresa (pantalla "Temas") con el que se pinta el documento.
    /// Nulo = tema personalizado: el que lleva guardado el propio preset.
    /// Es una referencia viva: cambiar el tema cambia todos los presets que
    /// lo usan, sin tocarlos.
    /// </summary>
    public string? ThemeId { get; set; }

    public ReportHeaderConfiguration? Header { get; set; }

    public List<ReportSectionConfiguration> Sections { get; set; } = [];

    public List<ReportBodyItemConfiguration> Body { get; set; } = [];

    public List<ReportFieldPlacementConfiguration> FieldPlacements { get; set; } = [];

    public ReportFooterConfiguration? Footer { get; set; }
}