using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Renderset.Web.Identity;

/// <summary>
/// Iniciar sesión con un tenant elegido. El tenant va como claim en la
/// cookie; si el usuario no pertenece a ninguno, no entra.
/// </summary>
public sealed class TenantSignIn(
    SignInManager<RendersetUser> signIn,
    UserManager<RendersetUser> users,
    TenantDirectory directory)
{
    /// <summary>
    /// El preferido si sigue siendo miembro; si no, el último con el que
    /// trabajó; si no, el primero por nombre.
    /// </summary>
    public async Task<TenantMembership?> PickTenantAsync(
        RendersetUser user,
        string? preferredTenantId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await directory.GetMembershipsAsync(user.Id, cancellationToken);

        return memberships.FirstOrDefault(x => Same(x.TenantId, preferredTenantId))
               ?? memberships.FirstOrDefault(x => Same(x.TenantId, user.LastTenantId))
               ?? memberships.FirstOrDefault();
    }

    /// <summary>
    /// Firma la cookie con el tenant. Devuelve false si el usuario no
    /// pertenece a ningún tenant activo.
    /// </summary>
    public async Task<bool> SignInAsync(
        RendersetUser user,
        bool persistent,
        string? preferredTenantId = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await PickTenantAsync(user, preferredTenantId, cancellationToken);

        if (tenant is null)
            return false;

        if (!Same(user.LastTenantId, tenant.TenantId))
        {
            user.LastTenantId = tenant.TenantId;
            await users.UpdateAsync(user);
        }

        await signIn.SignInWithClaimsAsync(
            user,
            persistent,
            [
                new Claim(RendersetClaimTypes.TenantId, tenant.TenantId),
                new Claim(RendersetClaimTypes.TenantName, tenant.TenantName),
                new Claim(RendersetClaimTypes.DisplayName, user.DisplayName ?? user.Email ?? user.UserName ?? string.Empty)
            ]);

        return true;
    }

    private static bool Same(
        string? a,
        string? b) =>
        !string.IsNullOrWhiteSpace(a) &&
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
