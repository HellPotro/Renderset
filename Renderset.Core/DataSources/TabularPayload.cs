using Renderset.Core.Reports;

namespace Renderset.Core.DataSources;

/// <summary>
/// Resultado tabular enviado por un cliente externo. Es el equivalente por
/// cable de <see cref="IDataSetReader"/>: exactamente lo que devuelve un
/// SELECT con JOINs, sin transformar.
///
/// Columnas y filas van separadas a propósito, en vez de un array de objetos.
/// Con mil filas ahorra repetir el nombre de cada columna mil veces, pero
/// sobre todo permite DECLARAR el tipo en lugar de adivinarlo: "00123" es una
/// referencia, no el número 123, y esa distinción se pierde si hay que
/// inferirla del valor.
/// </summary>
public sealed class TabularPayload
{
    public List<TabularPayloadColumn> Columns { get; set; } = [];

    /// <summary>
    /// Una lista de valores por fila, en el mismo orden que las columnas.
    /// </summary>
    public List<List<object?>> Rows { get; set; } = [];

    public DataSetSchema ToSchema() =>
        new()
        {
            Columns =
                Columns
                    .Select((column, index) => new DataSetColumn
                    {
                        Name = column.Name,
                        Ordinal = index,
                        Type = column.Type,
                        Nullable = column.Nullable
                    })
                    .ToList()
        };
}

public sealed class TabularPayloadColumn
{
    public required string Name { get; set; }

    public ReportDataType Type { get; set; } = ReportDataType.String;

    public bool Nullable { get; set; } = true;
}
