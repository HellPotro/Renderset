namespace Renderset.Core.DataSources;

/// <summary>
/// Un proveedor sabe abrir un origen concreto y devolver filas. Nada más.
/// Añadir Excel, REST o un ERP propietario es escribir una implementación de
/// esto: ni el Hierarchy Builder ni el diseñador ni el motor se enteran.
/// </summary>
public interface IDataSourceProvider
{
    /// <summary>
    /// Identificador del tipo de origen: "sql", "csv", "excel", "rest"...
    /// Es lo que se guarda en <see cref="DataSourceDefinition.Type"/>.
    /// </summary>
    string Type { get; }

    string DisplayName { get; }

    /// <summary>
    /// Abre el origen con la configuración dada. Los parámetros son los
    /// valores en tiempo de ejecución (el número de factura, el rango de
    /// fechas), no van en la configuración guardada.
    /// </summary>
    Task<IDataSetReader> OpenAsync(
        DataSourceDefinition definition,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lectura acotada para el asistente: mismas columnas, pocas filas.
    /// Separado de OpenAsync para que cada proveedor limite como sepa
    /// (TOP en SQL, primeras N líneas en CSV) en vez de traerse un millón de
    /// filas para enseñar diez.
    /// </summary>
    Task<IDataSetReader> PreviewAsync(
        DataSourceDefinition definition,
        IReadOnlyDictionary<string, object?> parameters,
        int maxRows,
        CancellationToken cancellationToken = default);
}
