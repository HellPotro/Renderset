namespace Renderset.Core.Sharing;

/// <summary>
/// Respuesta de GET /api/sharing/{tenantId}/settings: la configuración del
/// tenant más los límites del servidor, que la pantalla necesita para
/// validar y para enseñar qué valor se usa cuando no hay ninguno.
/// </summary>
public sealed class DocumentSharingSettingsResponse
{
    public required DocumentSharingSettings Settings { get; init; }

    public int MaxExpirationDays { get; init; }

    /// <summary>
    /// Caducidad de DocumentSharing, la que se aplica si el tenant no tiene
    /// una propia.
    /// </summary>
    public int SystemDefaultExpirationDays { get; init; }
}


/// <summary>
/// Petición de POST /api/sharing/{tenantId}/preview. Pinta la página con la
/// configuración que se está editando, sin guardarla.
/// </summary>
public sealed class DocumentSharingPreviewRequest
{
    public DocumentSharingSettings Settings { get; set; } = new();

    public string? Culture { get; set; }

    /// <summary>
    /// Pinta el aviso de enlace caducado en vez del visor.
    /// </summary>
    public bool Expired { get; set; }
}
