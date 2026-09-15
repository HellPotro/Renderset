namespace Renderset.Core.Mapping;

/// <summary>
/// Nodo del modelo jerárquico. Es recursivo desde el primer día a propósito:
/// tu ejemplo tiene un solo nivel (cabecera + líneas), pero en cuanto
/// aparezca un albarán con líneas y lotes por línea, o una factura con
/// vencimientos además de líneas, un modelo de "columnas de cabecera" y
/// "columnas de detalle" se queda corto y hay que rehacerlo.
/// </summary>
public sealed class DataMappingNode
{
    /// <summary>
    /// Nombre en el modelo. Vacío sólo en la raíz.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public DataMappingNodeKind Kind { get; set; } =
        DataMappingNodeKind.Object;

    /// <summary>
    /// Columnas que identifican un elemento de este nodo. En la raíz son las
    /// que identifican el documento (el número de factura). En una colección,
    /// las que distinguen una línea de otra.
    ///
    /// Si se dejan vacías en una colección, cada fila del origen produce un
    /// elemento, que es lo correcto cuando no hay una clave natural.
    /// </summary>
    public List<string> KeyColumns { get; set; } = [];

    public List<DataMappingField> Fields { get; set; } = [];

    public List<DataMappingNode> Children { get; set; } = [];

    /// <summary>
    /// Todas las columnas de origen que cuelgan de este nodo y sus hijos.
    /// Se usa para detectar filas vacías de LEFT JOIN.
    /// </summary>
    public IEnumerable<string> AllSourceColumns()
    {
        foreach (var field in Fields)
        {
            if (!string.IsNullOrWhiteSpace(field.SourceColumn))
                yield return field.SourceColumn;
        }

        foreach (var key in KeyColumns)
            yield return key;

        foreach (var child in Children)
        {
            foreach (var column in child.AllSourceColumns())
                yield return column;
        }
    }
}
