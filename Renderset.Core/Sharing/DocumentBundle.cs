namespace Renderset.Core.Sharing;

/// <summary>
/// Conjunto de documentos ya emitidos que se comparte con un único enlace:
/// la factura, el packing list y el certificado de origen de un mismo envío.
///
/// No guarda documentos, sólo referencias a <see cref="Rendering.RenderedDocument"/>.
/// Esos ya son inmutables, así que el bundle hereda la garantía: el cliente
/// ve exactamente lo que se le envió aunque después se retoque el diseño.
///
/// El enlace se compone de dos partes: el <see cref="Id"/>, que identifica, y
/// un token aleatorio, que autoriza. Para validar el acceso sólo se usa el
/// hash. Aparte se guarda el token cifrado (<see cref="ProtectedToken"/>)
/// para poder volver a abrir o copiar el enlace desde RenderSet; la clave
/// de cifrado no está en la base de datos, así que una copia de la base de
/// datos sola no da acceso a los enlaces.
/// </summary>
public sealed class DocumentBundle
{
    public required Guid Id { get; init; }

    public required string TenantId { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// Texto opcional que se enseña encima de los documentos: "Adjuntamos la
    /// documentación del pedido 4512".
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Idioma de la página contenedora (botones, avisos). Cada documento va
    /// en el idioma con el que se emitió.
    /// </summary>
    public required string Culture { get; init; }

    /// <summary>
    /// SHA-256 del token. Nunca el token en claro.
    /// </summary>
    public required byte[] TokenHash { get; init; }

    /// <summary>
    /// Token cifrado con <see cref="IBundleTokenProtector"/>. Nulo en los
    /// bundles creados antes de existir: su enlace no se puede recuperar y
    /// hay que generar uno nuevo.
    /// </summary>
    public string? ProtectedToken { get; init; }

    public DateTime TokenIssuedAtUtc { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public int AccessCount { get; init; }

    public DateTime? FirstAccessedAtUtc { get; init; }

    public DateTime? LastAccessedAtUtc { get; init; }

    public IReadOnlyList<DocumentBundleItem> Items { get; init; } = [];

    public DocumentBundleStatus GetStatus(
        DateTime nowUtc)
    {
        if (RevokedAtUtc is not null)
            return DocumentBundleStatus.Revoked;

        return nowUtc >= ExpiresAtUtc
            ? DocumentBundleStatus.Expired
            : DocumentBundleStatus.Active;
    }
}


public sealed class DocumentBundleItem
{
    /// <summary>
    /// Orden dentro del bundle, empezando en 1. Es lo que va en las URLs
    /// públicas en lugar del id del documento: el cliente no necesita conocer
    /// identificadores internos.
    /// </summary>
    public required int Position { get; init; }

    public required string DocumentId { get; init; }

    /// <summary>
    /// Nombre que ve el cliente en la lista: "Factura F-2026-0412". Se fija
    /// al crear el bundle para que la lista no dependa de leer cada documento.
    /// </summary>
    public required string DisplayName { get; init; }
}


public enum DocumentBundleStatus
{
    Active,
    Expired,
    Revoked
}
