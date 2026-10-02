namespace Renderset.Core.Rendering;

/// <summary>
/// Filtro del listado de documentos emitidos. Pensado para la pantalla de
/// consulta, no para exportar: siempre devuelve una página acotada, los más
/// recientes primero.
/// </summary>
public sealed class RenderedDocumentQuery
{
    public const int MaxTake = 200;

    /// <summary>
    /// Busca en el nombre del fichero y en el id del documento.
    /// </summary>
    public string? Search { get; set; }

    public string? ReportId { get; set; }

    public int Take { get; set; } = 50;
}
