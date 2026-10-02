using Renderset.Core.Sharing;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Construcción de las URLs públicas de un bundle. En un único sitio porque
/// las usan los endpoints de gestión (enlace que se manda al cliente) y el
/// visor (enlaces a cada documento).
/// </summary>
internal static class ShareLinks
{
    public const string Prefix = "/share";

    /// <summary>
    /// Enlace absoluto que se entrega al ERP para mandarlo al cliente. Usa
    /// DocumentSharing:PublicBaseUrl si está configurado: detrás de un proxy
    /// el host de la petición puede ser una IP interna que el cliente no
    /// puede abrir.
    /// </summary>
    public static string PublicBundleUrl(
        HttpContext httpContext,
        DocumentSharingOptions options,
        Guid bundleId,
        string token)
    {
        var baseUrl =
            string.IsNullOrWhiteSpace(options.PublicBaseUrl)
                ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}"
                : options.PublicBaseUrl.TrimEnd('/');

        return baseUrl + BundlePath(bundleId, token);
    }

    /// <summary>
    /// Ruta relativa a la raíz del sitio, para los enlaces dentro del visor:
    /// la página ya se está sirviendo desde aquí, así que no hace falta host.
    /// </summary>
    public static string LocalBundlePath(
        HttpContext httpContext,
        Guid bundleId,
        string token) =>
        httpContext.Request.PathBase + BundlePath(bundleId, token);

    public static string BundlePath(
        Guid bundleId,
        string token) =>
        $"{Prefix}/{bundleId:D}/{Uri.EscapeDataString(token)}";
}
