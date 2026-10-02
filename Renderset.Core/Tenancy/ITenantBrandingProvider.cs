namespace Renderset.Core.Tenancy;

public interface ITenantBrandingProvider
{
    /// <summary>
    /// Nunca devuelve null: un tenant sin marca configurada recibe su nombre
    /// con los colores por defecto.
    /// </summary>
    Task<TenantBranding> GetAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
