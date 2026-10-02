using System.Net;
using System.Text.RegularExpressions;

namespace Renderset.Core.Rendering;

/// <summary>
/// Localiza y sustituye las imágenes remotas (&lt;img src="https://..."&gt;)
/// del HTML de un documento.
///
/// Sólo trabaja sobre el HTML que genera el propio renderer, que siempre
/// escribe los atributos entre comillas dobles; no pretende ser un parser de
/// HTML arbitrario.
/// </summary>
public static partial class HtmlImageSources
{
    /// <summary>
    /// URLs remotas distintas, ya decodificadas (&amp;amp; → &amp;).
    /// </summary>
    public static IReadOnlyList<string> FindRemote(
        string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        return ImgSource()
            .Matches(html)
            .Select(x => WebUtility.HtmlDecode(x.Groups["url"].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Sustituye cada URL que tenga reemplazo (normalmente un data URI). Las
    /// que no lo tienen se quedan como estaban.
    /// </summary>
    public static string Replace(
        string html,
        IReadOnlyDictionary<string, string> replacements)
    {
        ArgumentNullException.ThrowIfNull(html);

        if (replacements.Count == 0)
            return html;

        return ImgSource().Replace(
            html,
            match =>
            {
                var url = WebUtility.HtmlDecode(match.Groups["url"].Value);

                return replacements.TryGetValue(url, out var replacement)
                    ? match.Groups["start"].Value +
                      WebUtility.HtmlEncode(replacement) +
                      match.Groups["end"].Value
                    : match.Value;
            });
    }

    [GeneratedRegex(
        """(?<start><img\b[^>]*?\bsrc\s*=\s*")(?<url>https?://[^"]+)(?<end>")""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImgSource();
}


/// <summary>
/// Incrusta en el HTML las imágenes remotas como data URI al emitir el
/// documento.
///
/// Sin esto, el logo es una URL: si mañana cambia la imagen en esa URL,
/// cambian todas las facturas ya enviadas, y el PDF depende de que el
/// conversor llegue a ese servidor.
/// </summary>
public interface IHtmlAssetInliner
{
    Task<string> InlineAsync(
        string html,
        CancellationToken cancellationToken = default);
}


/// <summary>
/// Sección "DocumentAssets" de la configuración de la API.
/// </summary>
public sealed class DocumentAssetsOptions
{
    public const string SectionName = "DocumentAssets";

    public bool InlineImages { get; set; } = true;

    public int MaxImageBytes { get; set; } = 2 * 1024 * 1024;

    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Permite descargar imágenes de direcciones internas (10.x, 192.168.x,
    /// localhost…). Desactivado por defecto: la URL del logo la escribe un
    /// usuario, y sin este freno la API serviría para sondear la red
    /// interna. Actívalo sólo si los logos están en un servidor de la
    /// intranet.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }
}
