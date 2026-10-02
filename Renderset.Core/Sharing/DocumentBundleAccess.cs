namespace Renderset.Core.Sharing;

/// <summary>
/// Un acceso al enlace público. Sirve para soporte ("el cliente dice que no
/// le ha llegado": sí, lo abrió el martes a las 10:14) y para saber si un
/// enlace se está usando antes de revocarlo.
/// </summary>
public sealed class DocumentBundleAccess
{
    public required Guid BundleId { get; init; }

    public required string TenantId { get; init; }

    public DocumentBundleAccessKind Kind { get; init; }

    public int? Position { get; init; }

    public DateTime AccessedAtUtc { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}


public enum DocumentBundleAccessKind
{
    /// <summary>Apertura de la página con la lista.</summary>
    View,

    /// <summary>Visualización de un documento dentro de la página.</summary>
    Document,

    /// <summary>Descarga de un documento suelto.</summary>
    Download,

    /// <summary>Descarga de todo el bundle en un ZIP.</summary>
    DownloadAll
}
