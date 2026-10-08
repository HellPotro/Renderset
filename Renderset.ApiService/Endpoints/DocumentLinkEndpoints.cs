using System.Net;
using System.Text;
using Renderset.Core.Rendering;
using Renderset.Core.Rendering.Pdf;
using Renderset.Core.Sharing;
using Renderset.Core.Tenancy;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Enlaces de documento que lleva el QR de la cabecera.
///
///     GET /d/{tenantId}/{documentId}/{firma}            visor público
///     GET /d/{tenantId}/{documentId}/{firma}/document   el documento (iframe)
///     GET /d/{tenantId}/{documentId}/{firma}/pdf        el PDF, como descarga ({pdfUrl} del QR)
///     GET /documents/{documentId}                       QR antiguos → visor de Web
///
/// El visor público lo abre cualquiera que lea el QR, sin cuenta: la
/// autorización es la firma (HMAC de tenant + documento, ver
/// <see cref="DocumentLinkOptions"/>). Es la misma página que los bundles,
/// con la marca del tenant, pero con un solo documento y sin caducidad.
/// Firma incorrecta = 404 neutro, como un token de bundle incorrecto.
///
/// Los datos incrustados del documento (CSV / JSON) no salen por aquí: un
/// QR impreso lo puede leer cualquiera, así que sólo se enseña lo que va en
/// el papel.
/// </summary>
public static class DocumentLinkEndpoints
{
    private const string LoggerCategory = "Renderset.DocumentLinks";

    private const string ViewerContentSecurityPolicy =
        "default-src 'none'; " +
        "style-src 'unsafe-inline'; " +
        "script-src 'unsafe-inline'; " +
        "img-src https: http: data:; " +
        "font-src data:; " +
        "frame-src 'self'; " +
        "base-uri 'none'; " +
        "form-action 'none'; " +
        "frame-ancestors 'self'";

    public static IEndpointRouteBuilder MapDocumentLinkRedirect(
        this IEndpointRouteBuilder app)
    {
        var links =
            app.MapGroup(DocumentLinkOptions.PublicPrefix)
                .ExcludeFromDescription()
                .AllowAnonymous()
                .RequireRateLimiting(SharingEndpoints.RateLimitPolicy)
                .AddEndpointFilter(async (context, next) =>
                {
                    ApplySecurityHeaders(context.HttpContext.Response);

                    return await next(context);
                });

        links.MapGet("/{tenantId}/{documentId}/{signature}", ViewAsync);
        links.MapGet("/{tenantId}/{documentId}/{signature}/document", DocumentAsync);
        links.MapGet("/{tenantId}/{documentId}/{signature}/pdf", PdfAsync);

        app.MapGet(
                "/documents/{documentId}",
                RedirectLegacy)
            .AllowAnonymous()
            .RequireRateLimiting(SharingEndpoints.RateLimitPolicy)
            .ExcludeFromDescription();

        return app;
    }

    // ------------------------------------------------------------- visor

    private static async Task<IResult> ViewAsync(
        string tenantId,
        string documentId,
        string signature,
        HttpContext http,
        DocumentLinkOptions links,
        IRenderedDocumentRepository documents,
        IDocumentSharingSettingsRepository sharingSettings,
        IDocumentBundleTextProvider bundleTexts,
        IDocumentBundlePageRenderer pages,
        IDocumentPdfService pdfService,
        CancellationToken cancellationToken)
    {
        http.Response.Headers.ContentSecurityPolicy = ViewerContentSecurityPolicy;

        var document =
            await OpenAsync(tenantId, documentId, signature, links, documents, cancellationToken);

        var settings =
            document is null
                ? null
                : await sharingSettings.GetAsync(tenantId, cancellationToken);

        if (document is null || settings is null)
            return await NotFoundAsync(http, pages, cancellationToken);

        var basePath =
            http.Request.PathBase +
            DocumentLinkOptions.PublicPath(tenantId, documentId, signature.Trim());

        var normalized = settings.Normalized();

        var view =
            new DocumentBundleView
            {
                Title = TitleOf(document.FileName),
                Message = null,
                FooterText = normalized.FooterText,
                Culture = document.Culture,
                ShowExpiry = false,
                Branding = settings.ToBranding(tenantId),
                Texts = await bundleTexts.GetAsync(
                    tenantId,
                    document.Culture,
                    cancellationToken),
                PdfAvailable = pdfService.IsAvailable,
                AllowDataDownload = false,
                DownloadAllUrl = $"{basePath}/document",
                DownloadAllPdfUrl = null,
                Documents =
                [
                    new DocumentBundleViewItem
                    {
                        Position = 1,
                        Title = TitleOf(document.FileName),
                        ViewUrl = $"{basePath}/document",
                        DownloadUrl = pdfService.IsAvailable
                            ? $"{basePath}/pdf"
                            : $"{basePath}/document?download=true"
                    }
                ]
            };

        var html =
            await pages.RenderViewerAsync(
                view,
                cancellationToken);

        return Results.Content(
            html,
            "text/html; charset=utf-8");
    }

    private static async Task<IResult> DocumentAsync(
        string tenantId,
        string documentId,
        string signature,
        bool? download,
        HttpContext http,
        DocumentLinkOptions links,
        IRenderedDocumentRepository documents,
        IDocumentBundlePageRenderer pages,
        CancellationToken cancellationToken)
    {
        var document =
            await OpenAsync(tenantId, documentId, signature, links, documents, cancellationToken);

        if (document is null)
        {
            http.Response.Headers.ContentSecurityPolicy = ViewerContentSecurityPolicy;
            return await NotFoundAsync(http, pages, cancellationToken);
        }

        // Sólo lo que va en el papel: fuera los datos incrustados.
        var content = DocumentEmbeddedData.Strip(document.Content);

        if (download == true)
        {
            return Results.File(
                Encoding.UTF8.GetBytes(content),
                "text/html; charset=utf-8",
                document.FileName);
        }

        http.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'self'";

        return Results.Content(
            content,
            "text/html; charset=utf-8");
    }

    private static async Task<IResult> PdfAsync(
        string tenantId,
        string documentId,
        string signature,
        HttpContext http,
        DocumentLinkOptions links,
        IRenderedDocumentRepository documents,
        IDocumentPdfService pdfService,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        if (!links.Verify(tenantId, documentId, signature))
            return Results.NotFound();

        // Un QR de descarga impreso no puede acabar en un 404 porque el
        // conversor no esté configurado: se descarga el documento en HTML.
        if (!pdfService.IsAvailable)
        {
            return Results.Redirect(
                http.Request.PathBase +
                DocumentLinkOptions.PublicPath(tenantId, documentId, signature.Trim()) +
                "/document?download=true");
        }

        byte[]? pdf;

        try
        {
            pdf =
                await pdfService.GetOrCreateAsync(
                    tenantId,
                    documentId,
                    cancellationToken);
        }
        catch (PdfConversionException ex)
        {
            loggers
                .CreateLogger(LoggerCategory)
                .LogError(ex, "No se ha podido generar el PDF del documento {DocumentId}.", documentId);

            return Results.Text(
                "No se ha podido generar el PDF en este momento. Inténtalo de nuevo en unos minutos.",
                "text/plain; charset=utf-8",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (pdf is null)
            return Results.NotFound();

        var document =
            await documents.GetByIdAsync(
                tenantId,
                documentId,
                cancellationToken);

        return Results.File(
            pdf,
            "application/pdf",
            Path.ChangeExtension(document?.FileName ?? "documento", ".pdf"));
    }

    // ----------------------------------------------------- QR antiguos

    /// <summary>
    /// Documentos emitidos con DocumentLinks:BaseUrl apuntando a la API: su
    /// QR trae https://&lt;api&gt;/documents/{id}. No llevan firma, así que no
    /// pueden abrir el visor público; se mandan al visor de Web (con sesión)
    /// o, si la configuración sigue mal, se explica qué cambiar.
    /// </summary>
    private static IResult RedirectLegacy(
        string documentId,
        HttpContext http,
        DocumentLinkOptions links,
        ILoggerFactory loggers)
    {
        var target = links.DocumentUrl(documentId);

        if (target is not null &&
            Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            !string.Equals(uri.Host, http.Request.Host.Host, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Redirect(target);
        }

        loggers
            .CreateLogger(LoggerCategory)
            .LogWarning(
                "Se ha abierto el enlace de un documento en la API ({Host}). DocumentLinks:BaseUrl " +
                "tiene que ser la URL pública de RenderSet Web, no la de la API. Valor actual: {BaseUrl}",
                http.Request.Host.Host,
                links.BaseUrl ?? "(vacío)");

        var html =
            $$"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>Documento - RenderSet</title>
            <style>
              body { margin:0; min-height:100vh; display:grid; place-items:center; background:#eef2f4;
                     font-family:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif; color:#23333c; }
              main { max-width:520px; margin:24px; padding:28px 30px; border-radius:12px; background:#fff;
                     box-shadow:0 2px 14px rgba(15,35,50,.12); }
              h1 { margin:0 0 10px; font-size:19px; }
              p { margin:8px 0; font-size:14px; line-height:1.5; color:#4c5f68; }
              code { padding:1px 5px; border-radius:4px; background:#f1f5f6; font-size:12.5px; }
            </style>
            </head>
            <body>
            <main>
              <h1>Este enlace no está bien configurado</h1>
              <p>El documento <code>{{WebUtility.HtmlEncode(documentId)}}</code> existe en RenderSet, pero el enlace
                 apunta a la API en lugar de al visor.</p>
              <p>Quien administre RenderSet tiene que poner en la API
                 <code>DocumentLinks__BaseUrl</code> con la dirección de RenderSet Web
                 (por ejemplo <code>https://app.renderset.app</code>).</p>
            </main>
            </body>
            </html>
            """;

        return Results.Content(
            html,
            "text/html; charset=utf-8",
            statusCode: StatusCodes.Status404NotFound);
    }

    // ------------------------------------------------------------ interno

    /// <summary>
    /// El documento si la firma es buena y existe; si no, nulo. Las dos
    /// cosas dan la misma respuesta para no revelar qué documentos existen.
    /// </summary>
    private static async Task<RenderedDocument?> OpenAsync(
        string tenantId,
        string documentId,
        string signature,
        DocumentLinkOptions links,
        IRenderedDocumentRepository documents,
        CancellationToken cancellationToken)
    {
        if (!links.Verify(tenantId, documentId, signature))
            return null;

        return await documents.GetByIdAsync(
            tenantId,
            documentId,
            cancellationToken);
    }

    private static async Task<IResult> NotFoundAsync(
        HttpContext http,
        IDocumentBundlePageRenderer pages,
        CancellationToken cancellationToken)
    {
        var culture = BrowserLanguage(http);

        var html =
            await pages.RenderUnavailableAsync(
                new DocumentBundleUnavailableView
                {
                    Reason = DocumentBundleOpenStatus.NotFound,
                    Culture = culture,
                    Branding = TenantBranding.Neutral(),
                    Texts = DocumentBundleTexts.For(culture)
                },
                cancellationToken);

        return Results.Content(
            html,
            "text/html; charset=utf-8",
            Encoding.UTF8,
            StatusCodes.Status404NotFound);
    }

    private static string TitleOf(
        string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);

        return string.IsNullOrWhiteSpace(name)
            ? fileName
            : name;
    }

    private static void ApplySecurityHeaders(
        HttpResponse response)
    {
        var headers = response.Headers;

        // La firma va en la URL: ni caché intermedia, ni Referer hacia el
        // dominio del logo, ni indexación.
        headers.CacheControl = "no-store";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Robots-Tag"] = "noindex, nofollow";
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "SAMEORIGIN";
    }

    private static string BrowserLanguage(
        HttpContext http)
    {
        var header = http.Request.Headers.AcceptLanguage.ToString();

        if (string.IsNullOrWhiteSpace(header))
            return "en";

        var first =
            header
                .Split(',', StringSplitOptions.RemoveEmptyEntries)[0]
                .Split(';')[0]
                .Trim();

        return string.IsNullOrWhiteSpace(first) || first == "*"
            ? "en"
            : first;
    }
}
