namespace Renderset.Core.Mapping;

/// <summary>
/// Mapping entre un resultado plano y el modelo jerárquico. Vive a nivel de
/// tenant y es independiente del report: varios diseños pueden compartirlo,
/// que es justo el objetivo.
/// </summary>
public sealed class DataMapping
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// Origen con el que se diseñó. El mapping sólo depende de los nombres de
    /// columna, así que puede reutilizarse con otro origen que devuelva las
    /// mismas columnas: la misma consulta contra otra base de datos, o un CSV
    /// exportado de ella.
    /// </summary>
    public string? DataSourceId { get; set; }

    public required DataMappingNode Root { get; set; }

    /// <summary>
    /// Report que usa este mapping cuando la petición trae filas y no dice
    /// qué mapping aplicar. Como mucho uno por report: asignar otro le quita
    /// la asignación al anterior.
    /// </summary>
    public string? ReportId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int Version { get; set; } = 1;
}
