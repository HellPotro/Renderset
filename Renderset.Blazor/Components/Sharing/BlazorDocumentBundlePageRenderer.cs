using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Renderset.Blazor.Components.Sharing;
using Renderset.Core.Sharing;
using Renderset.Core.Tenancy;
using System.Net;
using System.Text;

namespace Renderset.Blazor.Sharing;

/// <summary>
/// Pinta las páginas públicas de un bundle con el renderizador estático de
/// Blazor, igual que <see cref="Rendering.BlazorReportDocumentRenderer"/>
/// pinta los documentos.
/// </summary>
public sealed class BlazorDocumentBundlePageRenderer
    : IDocumentBundlePageRenderer
{
    private const string CssResourceName =
        "Renderset.Blazor.document-bundle.css";

    private const string ViewerCssResourceName =
        "Renderset.Blazor.document-viewer.css";

    private const string ViewerScriptResourceName =
        "Renderset.Blazor.document-viewer.js";

    /// <summary>
    /// CSS de la página y, detrás, el de la barra del visor, que es el mismo
    /// que usa el visor interno de Web.
    /// </summary>
    private static readonly Lazy<string> Css =
        new(() =>
            LoadResource(CssResourceName) +
            Environment.NewLine +
            LoadResource(ViewerCssResourceName));

    /// <summary>
    /// Script de la barra (compartido con Web) y, detrás, el de la lista de
    /// documentos de esta página.
    /// </summary>
    private static readonly Lazy<string> ViewerPageScript =
        new(() =>
            LoadResource(ViewerScriptResourceName) +
            Environment.NewLine +
            ListScript);

    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;

    public BlazorDocumentBundlePageRenderer(
        IServiceProvider services,
        ILoggerFactory loggerFactory)
    {
        _services = services;
        _loggerFactory = loggerFactory;
    }

    public async Task<string> RenderViewerAsync(
        DocumentBundleView view,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Documents.Count == 0)
        {
            throw new InvalidOperationException(
                "Un bundle sin documentos no se puede pintar en el visor.");
        }

        var body =
            await RenderComponentAsync<DocumentBundleViewer>(
                new Dictionary<string, object?>
                {
                    [nameof(DocumentBundleViewer.View)] = view
                });

        return BuildPage(
            view.Title,
            view.Texts.Language,
            view.Branding,
            body,
            ViewerPageScript.Value);
    }

    public async Task<string> RenderUnavailableAsync(
        DocumentBundleUnavailableView view,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(view);

        var body =
            await RenderComponentAsync<DocumentBundleUnavailable>(
                new Dictionary<string, object?>
                {
                    [nameof(DocumentBundleUnavailable.View)] = view
                });

        return BuildPage(
            view.Texts.UnavailableTitle,
            view.Texts.Language,
            view.Branding,
            body,
            script: null);
    }

    private async Task<string> RenderComponentAsync<TComponent>(
        Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using var renderer =
            new HtmlRenderer(
                _services,
                _loggerFactory);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output =
                await renderer.RenderComponentAsync<TComponent>(
                    ParameterView.FromDictionary(parameters));

            return output.ToHtmlString();
        });
    }

    private static string BuildPage(
        string title,
        string language,
        TenantBranding branding,
        string body,
        string? script)
    {
        var builder = new StringBuilder();

        builder.AppendLine("<!DOCTYPE html>");
        builder.Append("<html lang=\"")
            .Append(WebUtility.HtmlEncode(language))
            .AppendLine("\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\" />");
        builder.AppendLine(
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");

        // Enlaces con token: que no los indexe nadie aunque acaben pegados
        // en algún sitio público.
        builder.AppendLine("<meta name=\"robots\" content=\"noindex, nofollow\" />");
        builder.AppendLine("<meta name=\"referrer\" content=\"no-referrer\" />");

        // Favicon incrustado: la API no sirve ficheros estáticos y la CSP de
        // la página ya admite imágenes data:.
        builder.AppendLine(FaviconLink);

        builder.Append("<title>")
            .Append(WebUtility.HtmlEncode(title))
            .Append(" · ")
            .Append(WebUtility.HtmlEncode(branding.DisplayName))
            .AppendLine("</title>");

        builder.AppendLine("<style>");
        builder.AppendLine(Css.Value);

        // Los colores ya vienen validados por TenantBranding (#rgb o
        // #rrggbb), así que no pueden cerrar el bloque ni meter reglas.
        builder.AppendLine(":root {");
        builder.Append("    --rs-share-primary: ")
            .Append(branding.PrimaryColor)
            .AppendLine(";");
        builder.Append("    --rs-share-secondary: ")
            .Append(branding.SecondaryColor)
            .AppendLine(";");
        builder.AppendLine("}");

        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body class=\"rs-share-page\">");
        builder.AppendLine(body);

        if (!string.IsNullOrWhiteSpace(script))
        {
            builder.AppendLine("<script>");
            builder.AppendLine(script);
            builder.AppendLine("</script>");
        }

        builder.AppendLine("</body>");
        builder.AppendLine("</html>");

        return builder.ToString();
    }

    /// <summary>
    /// Lista de documentos: el cambio de documento lo hace el propio enlace
    /// con target al iframe; esto marca el activo y pone al día la barra.
    /// Imprimir, CSV y JSON son cosa de document-viewer.js.
    /// </summary>
    /// <summary>
    /// El mismo icono que RenderSet Web (Renderset.Web/wwwroot/favicon-32.png),
    /// en base64.
    /// </summary>
    private const string FaviconLink =
        "<link rel=\"icon\" type=\"image/png\" href=\"data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAEJElEQVR4nMWXy29VVRTGf2vtc0rfLbVQMAQNMFBjiIgJojT4GGgMj2JyB2qYEB34JIGBUUcmGgYkBDUmoiYadUCCkUKNiiMI9h9AJkotBLAIfXD7ou29Z+/l4NDaktKe25a4kpOcnHX2/r699lrfXlsAyOUcR474hs3Priij4k0LbIWwBogAYX5mQALaIUpbgZFP+k79cnkcU8Zflm7a0iIu/lTV3W3BYyHME3eqiSqijhB8l/ni69d++7GVXM4JQNPj27dKHB3HAuZDguCY/8pvNcPw4jRCFCsm2662H2uTxuYdy5VwRlUbzXuPiFtg4FtomBfnXAihJ6BrVcXv0ShuNJ/ceXAAEWc+8RrFjSp+TyRBWgxviC50yGcgoWLemwRpicBWYUHIsOcit//FzEqhoFgAbFWEiGYZYQaJT6afTYQoihAB70uoHhGNMoEDzimLa6un9Y8WCvT1D+B9oK6mihCyR2NWAqrCyMgY96++h58O7f+PlBlp1I0bo2P8eeES3xw/wQ+/nqKyonzhCKQQaQTqa6aPQEMdrGhawlMbHmbdfWt476Mvqa2uImQQs0z7D2kOpKtOnyvdvfxx/iJ/XeoimBHMSLxn984cmx95iMHhG6jOPn1mApBWgZkhIrx78AvW515h44uv8sLe9xkrFG4SNZ7b/CiFYhGdoWrmRGDKQFVEhYpFZRw/2c65C5eJXKpjjfV1qAhZUjFTDkxnQ8M3GMoPUCgUeWD1vaxYtgQfAk6VS/9cI5hlOkxKJjAuRm+89DxbntjIorIyHlv3IA11tQAk3vP9iZNUlpdnSsI5E2hev5bm9Wun+EbHCuze9zFnO85TV12FvxMEzEAEfj/XycplS6koLydyytmO8+x8+0M6L3dRW12ZCRzmkIRm6cTvHDjE/q8OUxZHGLByeRNx7Iij0pqoOVfBXfV1fHa4le6+PAD1NdXs3/sahWKxpE6mJAKThagsjujt7uXgt0dwqhSKCU9uWEfL05voGxicKMkFIyCSJqBzDhEhmFFeU83XrT/TcfFvyuI0nT5462UaamsoJAkZdCgbASE9ZvODQ1zvHyA/OMRYoUgcRQwMDbPv8+/IDw7RfT1P4+J6dm57hqHhkUxSLE3N22cVLAOcKrVVldhNQsMjo6ncquK9Z3FdLUK6TaLC9f7BGRuYcctUhuMR6Mn3T3xzqhNng3NK7yQfkDkHIsxClq5IhJslltrkFsxsqu9W/23NLChIJ6IGzKock6tgJl8G8JBiSqeaWqs4J1gJfdR8zYKJc2JqrRrMHQhJsUdc5DDzdx7cvLjIhaTYE8wd0J7TR69IYBeiiHMOI4FMR3nJ0BiJOOcQRQK7ek4fvaLkcu5q+7G2kIztMOjSOI5EF/6SIqqicRwZdIVkbMfV9mNtE5fT//N6/i+XFfKiPRLmEwAAAABJRU5ErkJggg==\" />";

    private const string ListScript =
        """
        (function () {

            var root = document.querySelector("[data-rs-viewer]");
            var items = document.querySelectorAll("[data-share-item]");

            if (!root)
                return;

            function select(item) {

                for (var i = 0; i < items.length; i++) {
                    items[i].classList.remove("is-active");
                    items[i].removeAttribute("aria-current");
                }

                item.classList.add("is-active");
                item.setAttribute("aria-current", "true");

                if (window.RenderSetViewer) {
                    window.RenderSetViewer.setDocument(root, {
                        title: item.getAttribute("data-title") || "",
                        viewUrl: item.href,
                        downloadUrl: item.getAttribute("data-download")
                    });
                }
            }

            for (var i = 0; i < items.length; i++) {
                items[i].addEventListener("click", function () {
                    select(this);
                });
            }

        })();
        """;

    private static string LoadResource(
        string resourceName)
    {
        var assembly =
            typeof(BlazorDocumentBundlePageRenderer).Assembly;

        using var stream =
            assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                $"No se ha encontrado el recurso '{resourceName}' del visor de " +
                "documentos. Comprueba que el fichero existe en " +
                "Renderset.Blazor/wwwroot y que el csproj lo incluye como " +
                "EmbeddedResource con ese LogicalName.");
        }

        using var reader =
            new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
