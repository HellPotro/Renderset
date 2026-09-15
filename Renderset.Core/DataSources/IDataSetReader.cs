namespace Renderset.Core.DataSources;

/// <summary>
/// Lectura hacia delante de un resultado tabular. Es la moneda común de toda
/// la capa de datos.
///
/// A propósito NO es System.Data.IDataReader: obligar a un CSV, a un Excel o
/// a una respuesta REST a implementar IDataReader entero es mucho trabajo
/// para nada. IDataReader se adapta a esto con veinte líneas
/// (<see cref="DataReaderDataSet"/>), y el resto de proveedores implementan
/// una interfaz de tres miembros.
/// </summary>
public interface IDataSetReader
    : IAsyncDisposable
{
    DataSetSchema Schema { get; }

    /// <summary>
    /// Avanza a la siguiente fila. False cuando se acaban.
    /// </summary>
    ValueTask<bool> ReadAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valor de la fila actual por posición de columna. Null para DBNull.
    /// </summary>
    object? GetValue(
        int ordinal);
}
