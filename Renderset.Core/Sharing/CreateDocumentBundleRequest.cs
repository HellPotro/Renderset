using Renderset.Core.Rendering;

namespace Renderset.Core.Sharing;

/// <summary>
/// Petición de POST /api/bundles/{tenantId}.
///
/// Cada elemento es, o un documento ya emitido (<see cref="CreateDocumentBundleItem.DocumentId"/>),
/// o una petición de render que se ejecuta en la misma llamada
/// (<see cref="CreateDocumentBundleItem.Render"/>). Las dos formas se pueden
/// mezclar: el ERP puede tener ya la factura emitida y pedir aquí el packing
/// list.
/// </summary>
public sealed class CreateDocumentBundleRequest
{
    public string? Title { get; set; }

    public string? Message { get; set; }

    /// <summary>
    /// Idioma de la página contenedora. Si no viene, el del primer documento.
    /// </summary>
    public string? Culture { get; set; }

    /// <summary>
    /// Fecha exacta de caducidad, en UTC. Excluyente con <see cref="ExpiresInDays"/>.
    /// </summary>
    public DateTime? ExpiresAtUtc { get; set; }

    public int? ExpiresInDays { get; set; }

    /// <summary>
    /// Deja al cliente descargar los datos de los documentos (CSV y JSON).
    /// Por defecto no: compartir el documento no implica compartir sus datos.
    /// </summary>
    public bool AllowDataDownload { get; set; }

    public List<CreateDocumentBundleItem> Items { get; set; } = [];
}


public sealed class CreateDocumentBundleItem
{
    public string? DocumentId { get; set; }

    public RenderRequest? Render { get; set; }

    /// <summary>
    /// Nombre en la lista del cliente. Si no viene, se usa el nombre del
    /// fichero sin extensión.
    /// </summary>
    public string? DisplayName { get; set; }
}


/// <summary>
/// Petición de POST /api/bundles/{tenantId}/{bundleId}/link: emite un token
/// nuevo, invalida el anterior y fija una caducidad nueva.
/// </summary>
public sealed class RenewDocumentBundleLinkRequest
{
    public DateTime? ExpiresAtUtc { get; set; }

    public int? ExpiresInDays { get; set; }
}
