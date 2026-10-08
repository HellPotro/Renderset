using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Renderset.Core.Tenancy;

namespace Renderset.Web.Identity;

/// <summary>
/// Tenant con el que trabaja el usuario que ha iniciado sesión: el que eligió
/// (claim de la cookie). Que siga siendo miembro lo comprueban la cookie en
/// cada petición, el circuito cada minuto (<see cref="TenantRevalidatingAuthenticationStateProvider"/>)
/// y <see cref="TenantTokenFactory"/> antes de cada llamada a la API.
///
/// Funciona igual en una petición normal (los endpoints de /files) que en un
/// circuito de Blazor, donde no hay HttpContext y el usuario sale del
/// AuthenticationStateProvider.
/// </summary>
public sealed class UserCurrentTenant(
    IHttpContextAccessor http,
    AuthenticationStateProvider? authentication = null)
    : ICurrentTenant
{
    public string Id =>
        User.FindFirstValue(RendersetClaimTypes.TenantId)
        ?? throw new InvalidOperationException(
            "No hay un tenant elegido: la página tendría que exigir sesión.");

    public string? Name =>
        User.FindFirstValue(RendersetClaimTypes.TenantName);

    public string? UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserDisplayName =>
        User.FindFirstValue(RendersetClaimTypes.DisplayName)
        ?? User.FindFirstValue(ClaimTypes.Email)
        ?? User.Identity?.Name;

    public bool IsAuthenticated =>
        User.Identity?.IsAuthenticated == true;

    /// <summary>
    /// Primero el estado de autenticación de Blazor: en un circuito el
    /// HttpContext es el de la conexión de SignalR y conserva el usuario de
    /// cuando se abrió, aunque la revalidación ya le haya sacado. Fuera de
    /// Blazor (los endpoints de /files) el proveedor no tiene estado y lanza:
    /// entonces, el de la petición.
    /// </summary>
    public ClaimsPrincipal User
    {
        get
        {
            Task<AuthenticationState>? state;

            try
            {
                state = authentication?.GetAuthenticationStateAsync();
            }
            catch (InvalidOperationException)
            {
                state = null;
            }

            // En un circuito y en el render del servidor el estado ya está
            // resuelto, así que la tarea viene completada.
            if (state is { IsCompletedSuccessfully: true })
                return state.Result.User;

            return http.HttpContext?.User
                   ?? new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
}
