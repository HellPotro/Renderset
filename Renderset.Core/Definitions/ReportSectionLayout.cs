namespace Renderset.Core.Definitions;

/// <summary>
/// Se serializa como texto, así que el orden de los valores no importa para
/// los presets guardados. Columns2/3/4 se mantienen sólo para leer presets
/// anteriores: el editor guarda siempre <see cref="Grid"/> con su número de
/// columnas en <c>Columns</c>.
/// </summary>
public enum ReportSectionLayout
{
    List,
    Columns2,
    Columns3,
    Columns4,
    Auto,

    /// <summary>
    /// Cuadrícula con el número de columnas de <c>Columns</c> (1 a 12).
    /// </summary>
    Grid
}
