namespace Renderset.Core.Definitions;

/// <summary>
/// Reglas comunes de la cuadrícula de campos de una sección. Las usan el
/// resolver, el renderer y los editores, para que todos entiendan igual los
/// presets antiguos (Columns2/3/4) y los nuevos (Grid + Columns).
/// </summary>
public static class ReportSectionGrid
{
    public const int MinColumns = 1;

    public const int MaxColumns = 12;

    public const int DefaultColumns = 2;

    /// <summary>
    /// A partir de aquí las celdas son estrechas y el renderer usa una
    /// variante más compacta (texto y relleno menores).
    /// </summary>
    public const int DenseFromColumns = 6;

    public static int Clamp(int? columns) =>
        Math.Clamp(
            columns ?? DefaultColumns,
            MinColumns,
            MaxColumns);

    /// <summary>
    /// Traduce lo guardado en el preset a la forma única con la que trabaja
    /// el resto: lista, o cuadrícula de N columnas.
    /// </summary>
    public static ReportSectionGridLayout Normalize(
        ReportSectionLayout? layout,
        int? columns)
    {
        return layout switch
        {
            null or ReportSectionLayout.List =>
                new(ReportSectionLayout.List, 1),

            ReportSectionLayout.Columns2 =>
                new(ReportSectionLayout.Grid, 2),

            ReportSectionLayout.Columns3 =>
                new(ReportSectionLayout.Grid, 3),

            ReportSectionLayout.Columns4 =>
                new(ReportSectionLayout.Grid, 4),

            // Auto nunca tuvo editor y se pintaba como una cuadrícula de una
            // columna. Se conserva ese aspecto.
            ReportSectionLayout.Auto =>
                new(ReportSectionLayout.Grid, columns is null ? 1 : Clamp(columns)),

            _ =>
                new(ReportSectionLayout.Grid, Clamp(columns))
        };
    }
}

public readonly record struct ReportSectionGridLayout(
    ReportSectionLayout Layout,
    int Columns)
{
    public bool IsList =>
        Layout == ReportSectionLayout.List;

    /// <summary>
    /// Columns sólo tiene sentido en cuadrícula: en lista se guarda nulo
    /// para no ensuciar el preset.
    /// </summary>
    public int? StoredColumns =>
        IsList ? null : Columns;

    public static ReportSectionGridLayout List { get; } =
        new(ReportSectionLayout.List, 1);
}
