using Renderset.Core.Security;
using Renderset.Core.Tenancy;
using Renderset.Web.Identity;

namespace Renderset.Web.Files;

/// <summary>
/// Documentos y PDF servidos a través de Web.
///
/// Antes las páginas enlazaban directamente a la API, pero la API ahora
/// exige una clave y un enlace del navegador no puede llevarla (ni debe: la
/// clave de servicio da acceso a todos los tenants). Web pide el fichero
/// con su clave, para el tenant del usuario, y lo devuelve tal cual.
///
///     GET /files/documents/{documentId}         HTML del documento (pestaña nueva)
///     GET /files/documents/{documentId}/embed   el mismo HTML, para el iframe del visor
///     GET /files/documents/{documentId}/pdf     PDF (?download=true para descargar)
///
/// El visor con el menú (imprimir, PDF, CSV, JSON) es la página
/// /documents/{documentId}.
/// </summary>
public static class DocumentFileEndpoints
{
    /// <summary>
    /// El HTML del documento se sirve desde el origen de Web: con "sandbox"
    /// el navegador lo trata como un origen aparte, así que sus scripts (la
    /// barra de exportar e imprimir) no pueden tocar la sesión de Web.
    /// </summary>
    private const string DocumentContentSecurityPolicy =
        "sandbox allow-scripts allow-popups allow-modals allow-downloads";

    /// <summary>
    /// Para el iframe del visor: del mismo origen, para que la página pueda
    /// imprimir sólo el documento y leer sus datos para el CSV, pero SIN
    /// scripts. Mismo origen y scripts a la vez anularían el sandbox; sin
    /// scripts el documento no puede hacer nada, y los documentos nuevos ya
    /// no traen ninguno.
    /// </summary>
    private const string EmbedContentSecurityPolicy =
        "sandbox allow-same-origin allow-modals; frame-ancestors 'self'";

    public static IEndpointRouteBuilder MapDocumentFileEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Con sesión: el tenant sale del usuario y cada petición a la API
        // lleva su token, así que un documento de otro tenant da 404/403
        // aunque se conozca su id.
        var files =
            app.MapGroup("/files/documents")
                .RequireAuthorization();

        files.MapGet(
            "/{documentId}",
            (string documentId, HttpContext context, DocumentFilesClient http, ICurrentTenant tenant, TenantTokenFactory tokens) =>
                ProxyAsync(
                    $"api/documents/{Uri.EscapeDataString(tenant.Id)}/{Uri.EscapeDataString(documentId)}",
                    context,
                    http,
                    tokens,
                    DocumentContentSecurityPolicy));

        files.MapGet(
            "/{documentId}/embed",
            (string documentId, HttpContext context, DocumentFilesClient http, ICurrentTenant tenant, TenantTokenFactory tokens) =>
                ProxyAsync(
                    $"api/documents/{Uri.EscapeDataString(tenant.Id)}/{Uri.EscapeDataString(documentId)}",
                    context,
                    http,
                    tokens,
                    EmbedContentSecurityPolicy));

        files.MapGet(
            "/{documentId}/pdf",
            (string documentId, bool? download, HttpContext context, DocumentFilesClient http, ICurrentTenant tenant, TenantTokenFactory tokens) =>
                ProxyAsync(
                    $"api/documents/{Uri.EscapeDataString(tenant.Id)}/{Uri.EscapeDataString(documentId)}/pdf" +
                    (download == true ? "?download=true" : string.Empty),
                    context,
                    http,
                    tokens,
                    contentSecurityPolicy: null));

        return app;
    }

    private static async Task<IResult> ProxyAsync(
        string path,
        HttpContext context,
        DocumentFilesClient files,
        TenantTokenFactory tokens,
        string? contentSecurityPolicy)
    {
        var isHtml = contentSecurityPolicy is not null;

        HttpResponseMessage response;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);

            if (await tokens.CreateAsync(context.RequestAborted) is { } token)
                request.Headers.Add(TenantTokenFormat.HeaderName, token);

            response =
                await files.Http.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    context.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            // Ya no es miembro del tenant de la sesión.
            return Results.Forbid();
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El navegador ha cancelado (iframe recargado, pestaña cerrada):
            // no hay a quién responder.
            return Results.Empty;
        }

        // La respuesta se libera cuando termina de enviarse al navegador.
        context.Response.RegisterForDispose(response);

        if (!response.IsSuccessStatusCode)
            return Results.StatusCode((int)response.StatusCode);

        context.Response.Headers.CacheControl = "private, no-store";

        if (contentSecurityPolicy is not null)
            context.Response.Headers.ContentSecurityPolicy = contentSecurityPolicy;

        var contentType =
            response.Content.Headers.ContentType?.ToString()
            ?? (isHtml ? "text/html; charset=utf-8" : "application/pdf");

        var fileName =
            response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');

        return Results.Stream(
            await response.Content.ReadAsStreamAsync(context.RequestAborted),
            contentType,
            fileName);
    }
}


/// <summary>
/// HttpClient único para /files, con la clave de servicio y sin la
/// resiliencia estándar (ver Program.cs).
/// </summary>
public sealed class DocumentFilesClient
    : IDisposable
{
    public DocumentFilesClient(
        Uri baseAddress,
        string apiKey)
    {
        Http =
            new HttpClient(
                new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                })
            {
                BaseAddress = baseAddress,
                Timeout = TimeSpan.FromSeconds(120)
            };

        Http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public HttpClient Http { get; }

    public void Dispose() =>
        Http.Dispose();
}
