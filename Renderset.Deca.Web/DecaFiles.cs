using Renderset.Core.Security;
using Renderset.Core.Tenancy;
using Renderset.Web.Identity;

namespace Renderset.Deca.Web;

/// <summary>
/// PDF de un DeCA a través del portal: con la sesión del usuario y sin
/// plazo, aunque el QR ya no descargue. La API comprueba el tenant con el
/// token, así que el DeCA de otro tenant da 404/403 aunque se sepa su id.
/// </summary>
public static class DecaFileEndpoints
{
    public static IEndpointRouteBuilder MapDecaFileEndpoints(
        this IEndpointRouteBuilder app)
    {
        var files =
            app.MapGroup("/files/deca")
                .RequireAuthorization();

        files.MapGet(
            "/{decaId}/pdf",
            (string decaId, int? version, HttpContext context, DecaFilesClient http, ICurrentTenant tenant, TenantTokenFactory tokens) =>
                ProxyAsync(
                    $"api/deca/{Uri.EscapeDataString(tenant.Id)}/{Uri.EscapeDataString(decaId)}/pdf?download=true" +
                    (version is { } v ? $"&version={v}" : string.Empty),
                    context,
                    http,
                    tokens));

        return app;
    }

    private static async Task<IResult> ProxyAsync(
        string path,
        HttpContext context,
        DecaFilesClient files,
        TenantTokenFactory tokens)
    {
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
            return Results.Empty;
        }

        context.Response.RegisterForDispose(response);

        if (!response.IsSuccessStatusCode)
            return Results.StatusCode((int)response.StatusCode);

        context.Response.Headers.CacheControl = "private, no-store";

        var fileName =
            response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');

        return Results.Stream(
            await response.Content.ReadAsStreamAsync(context.RequestAborted),
            "application/pdf",
            fileName);
    }
}

/// <summary>
/// HttpClient para las descargas, con la clave de servicio y sin la
/// resiliencia estándar de ServiceDefaults (corta a los 10 segundos).
/// </summary>
public sealed class DecaFilesClient
    : IDisposable
{
    public DecaFilesClient(
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
                Timeout = TimeSpan.FromSeconds(60)
            };

        Http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    public HttpClient Http { get; }

    public void Dispose() =>
        Http.Dispose();
}
