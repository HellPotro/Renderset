using Renderset.Core.Paging;

namespace Renderset.Core.Sharing;

/// <summary>
/// Filtro del listado de bundles, los más recientes primero y paginado por
/// cursor.
/// </summary>
public sealed class DocumentBundleQuery
{
    public const int MaxTake = 200;

    /// <summary>
    /// Busca en el título y en los documentos (nombre visible e id): sirve
    /// para responder "¿en qué enlace mandé la factura F-0412?".
    /// </summary>
    public string? Search { get; set; }

    public DocumentBundleStatus? Status { get; set; }

    /// <summary>
    /// Creados a partir de este instante, incluido.
    /// </summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>
    /// Creados antes de este instante, excluido.
    /// </summary>
    public DateTime? ToUtc { get; set; }

    public int Take { get; set; } = 50;

    public KeysetCursor? Cursor { get; set; }

    /// <summary>
    /// Instante con el que se decide si un bundle ha caducado al filtrar por
    /// estado. Lo pone quien construye la consulta para que el filtro y el
    /// estado que se devuelve usen el mismo reloj.
    /// </summary>
    public DateTime NowUtc { get; set; } = DateTime.UtcNow;
}
