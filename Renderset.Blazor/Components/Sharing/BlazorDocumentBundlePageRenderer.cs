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

    private static readonly Lazy<string> Css =
        new(LoadCss);

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
            ViewerScript);
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
    /// Sólo estado visual: el cambio de documento lo hace el propio enlace
    /// con target al iframe. Sin dependencias, como la barra del documento.
    /// </summary>
    private const string ViewerScript =
        """
        (function () {

            var frame = document.querySelector(".rs-share-frame");
            var items = document.querySelectorAll("[data-share-item]");
            var current = document.querySelector("[data-share-current]");
            var openLink = document.querySelector("[data-share-open]");
            var downloadLink = document.querySelector("[data-share-download]");
            var printButton = document.querySelector("[data-share-print]");

            if (!frame)
                return;

            function select(item) {

                for (var i = 0; i < items.length; i++) {
                    items[i].classList.remove("is-active");
                    items[i].removeAttribute("aria-current");
                }

                item.classList.add("is-active");
                item.setAttribute("aria-current", "true");

                var title = item.getAttribute("data-title") || "";

                if (current)
                    current.textContent = title;

                frame.title = title;

                if (openLink)
                    openLink.href = item.href;

                if (downloadLink)
                    downloadLink.href = item.getAttribute("data-download");
            }

            for (var i = 0; i < items.length; i++) {
                items[i].addEventListener("click", function () {
                    select(this);
                });
            }

            if (printButton) {
                printButton.addEventListener("click", function () {

                    /* El iframe es del mismo origen, así que se puede
                       imprimir sólo el documento y no la página entera. */
                    try {
                        frame.contentWindow.focus();
                        frame.contentWindow.print();
                    } catch (e) {
                        window.open(frame.src, "_blank", "noopener");
                    }
                });
            }

        })();
        """;

    private static string LoadCss()
    {
        var assembly =
            typeof(BlazorDocumentBundlePageRenderer).Assembly;

        using var stream =
            assembly.GetManifestResourceStream(CssResourceName);

        if (stream is null)
        {
            throw new InvalidOperationException(
                "No se ha encontrado el CSS del visor de documentos. Comprueba " +
                "que Renderset.Blazor/wwwroot/css/document-bundle.css existe y " +
                "que el csproj lo incluye como EmbeddedResource con LogicalName " +
                $"'{CssResourceName}'.");
        }

        using var reader =
            new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
