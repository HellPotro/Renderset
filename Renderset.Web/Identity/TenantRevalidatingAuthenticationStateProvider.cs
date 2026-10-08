using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Renderset.Web.Identity;

/// <summary>
/// Un circuito de Blazor vive mientras la pestaña esté abierta, sin pasar por
/// la cookie. Cada minuto se comprueba que el usuario sigue existiendo, que
/// no ha cambiado su sello de seguridad (contraseña) y que sigue siendo
/// miembro del tenant elegido. El bloqueo por intentos fallidos no cierra
/// sesiones: sólo frena a quien intenta entrar (si no, cualquiera podría
/// echar a otro fallando 5 veces su contraseña). Si no, la sesión del circuito se cae y
/// la página pide entrar de nuevo.
/// </summary>
public sealed class TenantRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopes,
    IOptions<IdentityOptions> options)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval =>
        TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState state,
        CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        var users = scope.ServiceProvider.GetRequiredService<UserManager<RendersetUser>>();
        var directory = scope.ServiceProvider.GetRequiredService<TenantDirectory>();

        var principal = state.User;
        var user = await users.GetUserAsync(principal);

        if (user is null)
            return false;

        if (users.SupportsUserSecurityStamp)
        {
            var stamp = principal.FindFirstValue(options.Value.ClaimsIdentity.SecurityStampClaimType);

            if (stamp != await users.GetSecurityStampAsync(user))
                return false;
        }

        var membership =
            await directory.GetMembershipAsync(
                user.Id,
                principal.FindFirstValue(RendersetClaimTypes.TenantId),
                cancellationToken);

        return membership is not null;
    }
}
