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

    internal List<int> ConstantOrdinals { get; init; } = [];

    internal List<int> VaryingOrdinals { get; init; } = [];
}
