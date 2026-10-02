using System.IO.Compression;
using System.Text;
using Renderset.Core.Sharing;
using Renderset.Core.Tenancy;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Enlace público de un bundle. Es lo único de la API pensado para que lo
/// abra alguien de fuera, sin credenciales: la autorización es el token.
///
///     GET /share/{bundleId}/{token}                                visor
///     GET /share/{bundleId}/{token}/documents/{position}           documento (iframe)
///     GET /share/{bundleId}/{token}/documents/{position}/download  documento como fichero
///     GET /share/{bundleId}/{token}/download                       todo en un ZIP
///
/// En las URLs va la posición del documento dentro del bundle y no su id:
/// el cliente no necesita conocer identificadores internos.
/// </summary>
public static class SharingEndpoints
{
    public const string RateLimitPolicy = "share";

    private const string LoggerCategory = "Renderset.Sharing";

    /// <summary>
    /// Política del visor: sus estilos y su script van incrustados, los
    /// documentos se cargan del mismo origen y el logo puede venir de fuera.
    /// </summary>
    private const string ViewerContentSecurityPolicy =
        "default-src 'none'; " +
        "style-src 'unsafe-inline'; " +
        "script-src 'unsafe-inline'; " +
        "img-src https: http: data:; " +
        "frame-src 'self'; " +
        "base-uri 'none'; " +
        "form-action 'none'; " +
        "frame-ancestors 'self'";

    public static IEndpointRouteBuilder MapSharingEndpoints(
        this IEndpointRouteBuilder app)
    {
        var share =
            app.MapGroup(ShareLinks.Prefix)
                .WithTags("Sharing")
                .ExcludeFromDescription()
                .RequireRateLimiting(RateLimitPolicy)
                .AddEndpointFilter(async (context, next) =>
                {
                    ApplySecurityHeaders(context.HttpContext.Response);

                    return await next(context);
                });

        share.MapGet(
            "/{bundleId:guid}/{token}",
            ViewAsync);

        share.MapGet(
            "/{bundleId:guid}/{token}/documents/{position:int}",
            DocumentAsync);

        share.MapGet(
            "/{bundleId:guid}/{token}/documents/{position:int}/download",
            DownloadDocumentAsync);

        share.MapGet(
            "/{bundleId:guid}/{token}/download",
            DownloadAllAsync);

        return app;
    }

    // ------------------------------------------------------------ visor

    private static async Task<IResult> ViewAsync(
        Guid bundleId,
        string token,
        HttpContext httpContext,
        IDocumentBundleService bundles,
        ITenantBrandingProvider branding,
        IDocumentBundlePageRenderer pages,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var open =
            await bundles.OpenAsync(
                bundleId,
                token,
                cancellationToken);

        httpContext.Response.Headers.ContentSecurityPolicy =
            ViewerContentSecurityPolicy;

        if (open.Status != DocumentBundleOpenStatus.Available)
        {
            return await UnavailableAsync(
                open,
                httpContext,
                branding,
                pages,
                cancellationToken);
        }

        var bundle = open.Bundle!;

        var tenantBranding =
            await branding.GetAsync(
                bundle.TenantId,
                cancellationToken);

        var basePath =
            ShareLinks.LocalBundlePath(
                httpContext,
                bundle.Id,
                token);

        var view =
            new DocumentBundleView
            {
                Title = bundle.Title,
                Message = bundle.Message,
                Culture = bundle.Culture,
                ExpiresAtUtc = bundle.ExpiresAtUtc,
                Branding = tenantBranding,
                Texts = DocumentBundleTexts.For(bundle.Culture),
                DownloadAllUrl = $"{basePath}/download",
                Documents = bundle.Items
                    .OrderBy(x => x.Position)
                    .Select(x => new DocumentBundleViewItem
                    {
                        Position = x.Position,
                        Title = x.DisplayName,
                        ViewUrl = $"{basePath}/documents/{x.Position}",
                        DownloadUrl = $"{basePath}/documents/{x.Position}/download"
                    })
                    .ToList()
            };

        var html =
            await pages.RenderViewerAsync(
                view,
                cancellationToken);

        await TryRecordAccessAsync(
            bundles,
            bundle,
            DocumentBundleAccessKind.View,
            position: null,
            httpContext,
            loggerFactory,
            cancellationToken);

        return Results.Content(
            html,
            "text/html; charset=utf-8");
    }

    // ------------------------------------------------------------ documentos

    private static async Task<IResult> DocumentAsync(
        Guid bundleId,
        string token,
        int position,
        HttpContext httpContext,
        IDocumentBundleService bundles,
        ITenantBrandingProvider branding,
        IDocumentBundlePageRenderer pages,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var open =
            await bundles.OpenAsync(
                bundleId,
                token,
                cancellationToken);

        if (open.Status != DocumentBundleOpenStatus.Available)
        {
            // Si el enlace caduca con la página abierta, el siguiente
            // documento que se pida enseña el aviso dentro del iframe en vez
            // de quedarse en blanco.
            httpContext.Response.Headers.ContentSecurityPolicy =
                ViewerContentSecurityPolicy;

            return await UnavailableAsync(
                open,
                httpContext,
                branding,
                pages,
                cancellationToken);
        }

        var bundled =
            await bundles.GetDocumentAsync(
                open.Bundle!,
                position,
                cancellationToken);

        if (bundled is null)
            return Results.NotFound();

        // El documento trae sus propios estilos y scripts incrustados; aquí
        // sólo se limita quién puede meterlo en un iframe.
        httpContext.Response.Headers.ContentSecurityPolicy =
            "frame-ancestors 'self'";

        await TryRecordAccessAsync(
            bundles,
            open.Bundle!,
            DocumentBundleAccessKind.Document,
            position,
            httpContext,
            loggerFactory,
            cancellationToken);

        return Results.Content(
            bundled.Document.Content,
            "text/html; charset=utf-8");
    }

    private static async Task<IResult> DownloadDocumentAsync(
        Guid bundleId,
        string token,
        int position,
        HttpContext httpContext,
        IDocumentBundleService bundles,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var open =
            await bundles.OpenAsync(
                bundleId,
                token,
                cancellationToken);

        if (open.Status != DocumentBundleOpenStatus.Available)
            return Results.NotFound();

        var bundled =
            await bundles.GetDocumentAsync(
                open.Bundle!,
                position,
                cancellationToken);

        if (bundled is null)
            return Results.NotFound();

        await TryRecordAccessAsync(
            bundles,
            open.Bundle!,
            DocumentBundleAccessKind.Download,
            position,
            httpContext,
            loggerFactory,
            cancellationToken);

        return Results.File(
            Encoding.UTF8.GetBytes(bundled.Document.Content),
            "text/html; charset=utf-8",
            bundled.Document.FileName);
    }

    private static async Task<IResult> DownloadAllAsync(
        Guid bundleId,
        string token,
        HttpContext httpContext,
        IDocumentBundleService bundles,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var open =
            await bundles.OpenAsync(
                bundleId,
                token,
                cancellationToken);

        if (open.Status != DocumentBundleOpenStatus.Available)
            return Results.NotFound();

        var bundle = open.Bundle!;

        var documents =
            await bundles.GetDocumentsAsync(
                bundle,
                cancellationToken);

        if (documents.Count == 0)
            return Results.NotFound();

        var zip =
            await BuildZipAsync(
                documents,
                cancellationToken);

        await TryRecordAccessAsync(
            bundles,
            bundle,
            DocumentBundleAccessKind.DownloadAll,
            position: null,
            httpContext,
            loggerFactory,
            cancellationToken);

        return Results.File(
            zip,
            "application/zip",
            SafeFileName(bundle.Title, "documentos") + ".zip");
    }

    // ------------------------------------------------------------ interno

    private static async Task<IResult> UnavailableAsync(
        DocumentBundleOpenResult open,
        HttpContext httpContext,
        ITenantBrandingProvider branding,
        IDocumentBundlePageRenderer pages,
        CancellationToken cancellationToken)
    {
        // Con token incorrecto no se sabe (ni se debe decir) de quién era el
        // enlace: marco neutro e idioma del navegador. Con token correcto
        // pero caducado o revocado, la marca y el idioma del bundle.
        var known = open.Bundle is not null;

        var culture =
            known
                ? open.Bundle!.Culture
                : BrowserLanguage(httpContext);

        var tenantBranding =
            known
                ? await branding.GetAsync(
                    open.Bundle!.TenantId,
                    cancellationToken)
                : TenantBranding.Neutral();

        var html =
            await pages.RenderUnavailableAsync(
                new DocumentBundleUnavailableView
                {
                    Reason = open.Status,
                    Culture = culture,
                    Branding = tenantBranding,
                    Texts = DocumentBundleTexts.For(culture)
                },
                cancellationToken);

        return Results.Content(
            html,
            "text/html; charset=utf-8",
            Encoding.UTF8,
            open.Status == DocumentBundleOpenStatus.NotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status410Gone);
    }

    private static async Task<byte[]> BuildZipAsync(
        IReadOnlyList<BundledDocument> documents,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();

        using (var archive =
                   new ZipArchive(
                       buffer,
                       ZipArchiveMode.Create,
                       leaveOpen: true))
        {
            foreach (var bundled in documents)
            {
                // El número delante mantiene el orden del bundle al abrir el
                // ZIP y evita choques si dos documentos se llaman igual.
                var name =
                    $"{bundled.Item.Position:00}-" +
                    SafeFileName(bundled.Document.FileName, "documento.html");

                var entry =
                    archive.CreateEntry(
                        name,
                        CompressionLevel.Optimal);

                await using var stream = entry.Open();

                await stream.WriteAsync(
                    Encoding.UTF8.GetBytes(bundled.Document.Content),
                    cancellationToken);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// El registro de accesos es informativo: si falla, el cliente tiene que
    /// poder ver sus documentos igual.
    /// </summary>
    private static async Task TryRecordAccessAsync(
        IDocumentBundleService bundles,
        DocumentBundle bundle,
        DocumentBundleAccessKind kind,
        int? position,
        HttpContext httpContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await bundles.RecordAccessAsync(
                bundle,
                kind,
                position,
                httpContext.Connection.RemoteIpAddress?.ToString(),
                httpContext.Request.Headers.UserAgent.ToString(),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory
                .CreateLogger(LoggerCategory)
                .LogWarning(
                    ex,
                    "No se ha podido registrar el acceso {Kind} al bundle {BundleId}.",
                    kind,
                    bundle.Id);
        }
    }

    private static void ApplySecurityHeaders(
        HttpResponse response)
    {
        var headers = response.Headers;

        // El token va en la URL: ni caché intermedia, ni Referer hacia el
        // dominio del logo, ni indexación.
        headers.CacheControl = "no-store";
        headers["Referrer-Policy"] = "no-referrer";
        headers["X-Robots-Tag"] = "noindex, nofollow";
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "SAMEORIGIN";
    }

    private static string BrowserLanguage(
        HttpContext httpContext)
    {
        var header =
            httpContext.Request.Headers.AcceptLanguage.ToString();

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

    private static string SafeFileName(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var invalid = Path.GetInvalidFileNameChars();

        var cleaned =
            new string(
                value
                    .Select(c => invalid.Contains(c) ? '-' : c)
                    .ToArray())
                .Trim()
                .Trim('.');

        if (cleaned.Length > 120)
            cleaned = cleaned[..120];

        return string.IsNullOrWhiteSpace(cleaned)
            ? fallback
            : cleaned;
    }
}
