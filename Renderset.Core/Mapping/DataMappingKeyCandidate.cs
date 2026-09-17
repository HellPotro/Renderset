namespace Renderset.Core.Mapping;

public sealed class DataMappingKeyCandidate
{
    public required string Column { get; init; }

    /// <summary>
    /// Posición de la columna. Se trabaja por posición y no por nombre
    /// porque una consulta con JOINs puede traer nombres repetidos
    /// (Referencia del pedido y Referencia del artículo, por ejemplo).
    /// </summary>
    public int Ordinal { get; init; }

    public int GroupCount { get; init; }

    public int Score { get; init; }

    public IReadOnlyList<string> ConstantColumns { get; init; } = [];

    public IReadOnlyList<string> VaryingColumns { get; init; } = [];

    /// <summary>
    /// Columnas que quedan constantes dentro de cada grupo, es decir, las
    /// que pertenecen a este nivel. Son públicas porque el asistente de la
    /// pantalla las usa para proponer la selección paso a paso.
    /// </summary>
    public IReadOnlyList<int> ConstantOrdinals { get; init; } = [];

    /// <summary>
    /// Columnas que siguen variando dentro del grupo, es decir, las que
    /// corresponden al detalle de debajo.
    /// </summary>
    public IReadOnlyList<int> VaryingOrdinals { get; init; } = [];
}
