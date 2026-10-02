namespace Renderset.Core.Sharing;

public interface IDocumentBundleRepository
{
    Task CreateAsync(
        DocumentBundle bundle,
        CancellationToken cancellationToken = default);

    Task<DocumentBundle?> GetAsync(
        string tenantId,
        Guid bundleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Búsqueda sin tenant, sólo para el enlace público: quien abre el enlace
    /// no sabe de qué tenant es. La autorización la da el token, que se
    /// comprueba después contra el hash.
    /// </summary>
    Task<DocumentBundle?> FindAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentBundle>> ListAsync(
        string tenantId,
        int take,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        string tenantId,
        Guid bundleId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sustituye el token y la caducidad, y quita la revocación si la había:
    /// el enlace viejo deja de valer igualmente porque su hash ya no está.
    /// </summary>
    Task<bool> ReplaceLinkAsync(
        string tenantId,
        Guid bundleId,
        byte[] tokenHash,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default);

    Task RecordAccessAsync(
        DocumentBundleAccess access,
        CancellationToken cancellationToken = default);
}
