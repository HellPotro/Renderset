namespace Renderset.Core.Rendering;

/// <summary>
/// Filtro del listado de documentos emitidos, los más recientes primero y
/// paginado por cursor (<see cref="Paging.KeysetCursor"/>).
/// </summary>
public sealed class RenderedDocumentQuery
{
    public const int MaxTake = 200;

    /// <summary>
    /// Busca en el nombre del fichero y en el id del documento.
    /// </summary>
    public string? Search { get; set; }

    public string? ReportId { get; set; }

    /// <summary>
    /// Emitidos a partir de este instante, incluido.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Emitidos antes de este instante, excluido. Para "hasta el día 5"
    /// incluido se pasa el inicio del día 6.
    /// </summary>
    public DateTime? ToUtc { get; set; }

    public int Take { get; set; } = 50;

    /// <summary>
    /// Ya decodificado: el endpoint rechaza los inválidos antes de llegar
    /// aquí.
    /// </summary>
    public Paging.KeysetCursor? Cursor { get; set; }
}
