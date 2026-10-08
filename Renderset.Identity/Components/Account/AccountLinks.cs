using Microsoft.AspNetCore.Components;

namespace Renderset.Web.Components.Account;

public static class AccountLinks
{
    /// <summary>
    /// Sólo rutas de esta aplicación: un returnUrl a otro dominio sería una
    /// redirección abierta.
    /// </summary>
    public static string SafeReturnUrl(
        string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) &&
        returnUrl.StartsWith('/') &&
        !returnUrl.StartsWith("//") &&
        !returnUrl.StartsWith("/\\") &&
        !returnUrl.Any(c => char.IsControl(c) || c == '\\') &&
        !returnUrl.StartsWith("/account", StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : "/";

    /// <summary>
    /// URL pública de Web para los enlaces de los correos. Se toma de
    /// App:PublicUrl y no de la petición: con la cabecera Host de la
    /// petición, alguien podría conseguir que el enlace de restablecer la
    /// contraseña apuntase a su dominio. Sin configurar (desarrollo), la de
    /// la petición.
    /// </summary>
    public static string PublicBaseUrl(
        IConfiguration configuration,
        NavigationManager navigation)
    {
        var configured = configuration["App:PublicUrl"];

        var baseUrl =
            !string.IsNullOrWhiteSpace(configured)
                ? configured
                : navigation.BaseUri;

        return baseUrl.TrimEnd('/');
    }
}
