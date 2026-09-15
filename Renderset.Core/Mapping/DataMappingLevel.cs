namespace Renderset.Core.Mapping;

/// <summary>
/// Un nivel detectado en la jerarquía. La pantalla lo usa para pintar el
/// árbol y para ofrecer, en cada columna, a qué nivel pertenece.
/// </summary>
public sealed class DataMappingLevel
{
    /// <summary>
    /// Ruta en el modelo: cadena vacía para la raíz, "albaran" para el primer
    /// nivel, "albaran.lines" para el segundo.
    /// </summary>
    public required string Path { get; init; }

    public required string Name { get; init; }

    public DataMappingNodeKind Kind { get; init; }

    /// <summary>
    /// Columna que identifica un elemento de este nivel. Nula en el nivel
    /// más profundo, donde cada fila es un elemento.
    /// </summary>
    public string? KeyColumn { get; init; }

    public IReadOnlyList<string> Columns { get; init; } = [];

    public string Label =>
        string.IsNullOrEmpty(Path)
            ? "Cabecera"
            : Path;
}
