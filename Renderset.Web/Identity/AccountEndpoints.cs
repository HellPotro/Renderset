using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Renderset.Web.Identity;

/// <summary>
/// Lo que toca la cookie y por tanto no puede hacerse desde un circuito de
/// Blazor: salir y cambiar de tenant. Formularios POST con antiforgery.
/// </summary>
public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(
        this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/account");

        account.MapPost(
            "/logout",
            async (SignInManager<RendersetUser> signIn) =>
            {
                await signIn.SignOutAsync();

                return TypedResults.LocalRedirect("~/account/login");
            })
            // Sin antiforgery a propósito: el token va ligado al usuario y, si
            // la sesión ya se ha caído (le han quitado del tenant, ha cambiado
            // la contraseña en otro sitio), "Salir" daría un 400. Lo peor que
            // puede hacer otra web con esto es cerrarte la sesión.
            .DisableAntiforgery();

        account.MapPost(
            "/tenant",
            async (
                HttpContext context,
                [FromForm] string tenantId,
                [FromForm] string? returnUrl,
                UserManager<RendersetUser> users,
                TenantDirectory directory,
                TenantSignIn signIn) =>
            {
                var user = await users.GetUserAsync(context.User);

                if (user is null)
                    return Results.LocalRedirect("~/account/login");

                // El tenant pedido tiene que ser uno del usuario: si no, se
                // queda donde estaba.
                if (await directory.GetMembershipAsync(user.Id, tenantId) is null)
                    return Results.LocalRedirect("~/");

                var current =
                    await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);

                await signIn.SignInAsync(
                    user,
                    current.Properties?.IsPersistent == true,
                    tenantId);

                return Results.LocalRedirect(SafeReturnUrl(returnUrl));
            })
            .RequireAuthorization();

        return app;
    }

    /// <summary>
    /// Sólo rutas locales: un returnUrl a otro dominio sería una redirección
    /// abierta.
    /// </summary>
    public static string SafeReturnUrl(
        string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "~/";

        // Caracteres de control o barras invertidas: LocalRedirect los
        // rechazaría con un 500.
        if (returnUrl.Any(c => char.IsControl(c) || c == '\\'))
            return "~/";

        if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))
            return "~" + returnUrl;

        return "~/";
    }
}
