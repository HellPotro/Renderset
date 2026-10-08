namespace Renderset.Core.Definitions;

/// <summary>
/// Cálculo del pie de una columna de tabla.
/// </summary>
public enum ReportColumnTotal
{
    None,

    /// <summary>Suma de los valores numéricos.</summary>
    Sum,

    /// <summary>Número de filas con valor (las vacías no cuentan).</summary>
    Count,

    Average,

    Min,

    Max
}
