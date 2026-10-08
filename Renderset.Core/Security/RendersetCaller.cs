namespace Renderset.Core.Security;

/// <summary>
/// Quién llama a la API y a qué tenants puede tocar.
///
/// Hay dos tipos de llamante:
///
/// - Una clave de tenant (el ERP de un cliente): sólo su tenant. Si la ruta
///   pide otro, se rechaza aunque la clave sea válida.
/// - Un servicio de confianza (RenderSet Web). Con el token de usuario
///   (<see cref="TenantTokenFormat.HeaderName"/>) sólo puede tocar el tenant
///   del token, con el rol del usuario. Sin token, cualquier tenant: sólo
///   se admite si Security:RequireUserToken está apagado (desarrollo).
/// </summary>
public sealed class RendersetCaller
{
    public required RendersetCallerKind Kind { get; init; }

    /// <summary>
    /// Tenant de la clave o del token de usuario. Nulo sólo para un servicio
    /// sin token.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Usuario de Web por el que llama el servicio (token). Nulo para claves
    /// de tenant y servicios sin token.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Rol del usuario en el tenant (token). Nulo si no hay usuario.
    /// </summary>
    public Tenancy.TenantRole? Role { get; init; }

    /// <summary>
    /// Nombre de la clave o del servicio, para el log.
    /// </summary>
    public required string Name { get; init; }

    public bool CanAccessTenant(
        string? routeTenantId)
    {
        // Un servicio sin token puede con todos; con token, sólo con el del
        // usuario, igual que una clave de tenant.
        if (Kind == RendersetCallerKind.Service && TenantId is null)
            return true;

        return !string.IsNullOrWhiteSpace(routeTenantId) &&
               string.Equals(
                   TenantId,
                   routeTenantId,
                   StringComparison.OrdinalIgnoreCase);
    }

    public bool IsService =>
        Kind == RendersetCallerKind.Service;

    /// <summary>
    /// Para lo que sólo pueden hacer administradores (claves, miembros,
    /// página pública). Una llamada sin usuario (servicio sin token) se
    /// considera de confianza; una clave de tenant, no.
    /// </summary>
    public bool HasRole(
        Tenancy.TenantRole required) =>
        Kind == RendersetCallerKind.Service &&
        (Role is null ? TenantId is null : Role >= required);
}


public enum RendersetCallerKind
{
    Tenant,
    Service
}
