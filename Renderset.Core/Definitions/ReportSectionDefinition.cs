namespace Renderset.Core.Definitions;

public sealed class ReportSectionDefinition
{
    public string Id { get; init; } = default!;

    public string Name { get; init; } = default!;

    public int Order { get; init; }

    public bool VisibleByDefault { get; init; } = true;

    /// <summary>
    /// Si está informado, la sección se repite una vez por cada elemento de
    /// esa colección, y todo lo de dentro (campos, tabla y subsecciones) se
    /// resuelve relativo al elemento.
    ///
    /// Es lo que permite factura → albaranes → líneas: una tabla no puede
    /// contener otra tabla, pero una sección repetida sí puede contener una
    /// tabla y más subsecciones.
    /// </summary>
    public string? DataPath { get; init; }

    public List<ReportFieldDefinition> Fields { get; init; } = [];

    public ReportTableDefinition? Table { get; init; }

    /// <summary>
    /// Subsecciones. Sus rutas son relativas a esta sección cuando esta
    /// repite.
    /// </summary>
    public List<ReportSectionDefinition> Sections { get; init; } = [];
}
