using System.Text.Json;
using Renderset.Core.Mapping;
using Renderset.Core.Reports;

namespace Renderset.Core.DataSources;

public enum DatasetSourceKind
{
    /// <summary>
    /// JSON jerárquico pegado directamente.
    /// </summary>
    Json,

    /// <summary>
    /// Resultado tabular pegado: SQL, CSV o Excel.
    /// </summary>
    Tabular
}

/// <summary>
/// Lo que produce el selector de origen, venga de donde venga.
///
/// Es la pieza que unifica los tres sitios que hoy hacen lo mismo con código
/// distinto: playground, alta de report y pruebas de dataset. Todos necesitan
/// un esquema y unos datos de ejemplo, y les da igual si salieron de un JSON
/// o de un SELECT con JOINs.
/// </summary>
public sealed class DatasetResult
{
    public required DatasetSourceKind Kind { get; init; }

    public required ReportDataSchema Schema { get; init; }

    /// <summary>
    /// Primer documento. Es lo que se guarda como SampleData del report y lo
    /// que alimenta el preview del diseñador.
    /// </summary>
    public required JsonElement SampleData { get; init; }

    /// <summary>
    /// Todos los documentos generados. Con JSON siempre es uno; con una
    /// consulta pueden ser muchos.
    /// </summary>
    public IReadOnlyList<JsonElement> Documents { get; init; } = [];

    /// <summary>
    /// Nulo cuando el origen es JSON: no hay nada que mapear porque ya viene
    /// jerárquico. Informado cuando es tabular, y es lo que habrá que
    /// guardar para que el cliente pueda enviar filas planas por API.
    /// </summary>
    public DataMapping? Mapping { get; init; }

    /// <summary>
    /// Texto original, por si el consumidor quiere conservarlo.
    /// </summary>
    public string? RawInput { get; init; }
}
