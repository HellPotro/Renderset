using System.Text.Json;

namespace Renderset.Core.DataSources;

/// <summary>
/// Adaptador que mete un <see cref="TabularPayload"/> recibido por API en el
/// mismo camino que cualquier otro origen. Gracias a esto el servidor no
/// tiene código específico para "datos que llegan por HTTP": son filas, como
/// las de SQL o las de un CSV.
/// </summary>
public sealed class TabularPayloadDataSet
    : IDataSetReader
{
    private readonly TabularPayload _payload;
    private int _index = -1;

    public TabularPayloadDataSet(
        TabularPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        _payload = payload;
        Schema = payload.ToSchema();
    }

    public DataSetSchema Schema { get; }

    public ValueTask<bool> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _index++;

        return ValueTask.FromResult(
            _index < _payload.Rows.Count);
    }

    public object? GetValue(
        int ordinal)
    {
        var row = _payload.Rows[_index];

        if (ordinal >= row.Count)
            return null;

        return Normalize(row[ordinal]);
    }

    public ValueTask DisposeAsync() =>
        ValueTask.CompletedTask;

    /// <summary>
    /// El deserializador deja JsonElement. Se convierte una vez aquí para que
    /// el HierarchyBuilder trabaje siempre con tipos CLR, igual que con un
    /// IDataReader.
    /// </summary>
    private static object? Normalize(
        object? value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,

            JsonValueKind.Number =>
                element.TryGetInt64(out var integer)
                    ? integer
                    : element.GetDecimal(),

            _ => element.GetString()
        };
    }
}
