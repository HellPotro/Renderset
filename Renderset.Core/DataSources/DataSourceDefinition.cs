namespace Renderset.Core.DataSources;

/// <summary>
/// Configuración guardada de un origen. La configuración concreta va como
/// JSON opaco porque cada proveedor tiene la suya.
/// </summary>
public sealed class DataSourceDefinition
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Coincide con <see cref="IDataSourceProvider.Type"/>.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Nombre de la conexión, no la cadena de conexión. Las credenciales
    /// viven en la configuración del servidor o en un secret store, nunca en
    /// una fila de base de datos que se edita desde una pantalla web.
    /// </summary>
    public string? ConnectionName { get; set; }

    /// <summary>
    /// Configuración específica del proveedor: la consulta SQL, el
    /// delimitador del CSV, la hoja del Excel, la URL del REST.
    /// </summary>
    public string ConfigurationJson { get; set; } = "{}";

    public List<DataSourceParameter> Parameters { get; set; } = [];

    public int Version { get; set; } = 1;
}
