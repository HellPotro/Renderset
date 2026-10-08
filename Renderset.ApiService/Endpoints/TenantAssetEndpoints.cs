using Renderset.Api.Security;
using Renderset.Core.Rendering;
using Renderset.Core.Tenancy;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Imágenes de los tenants (el logo).
///
///     POST /api/assets/{tenantId}/logo        subir (multipart, campo "file"); devuelve la URL pública
///     GET  /assets/{tenantId}/{id}.{ext}      la imagen, pública y cacheable para siempre
///
/// Subir no cambia nada más: la pantalla que sube (Página pública, Marca)
/// pone la URL en el logo y lo guarda con el resto de la marca.
/// </summary>
public static class TenantAssetEndpoints
{
    public const string PublicPrefix = "/assets";

    public static IEndpointRouteBuilder MapTenantAssetEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/assets")
            .WithTags("Assets")
            .MapPost("/{tenantId}/logo", UploadLogoAsync)
            .RequireTenantRole(TenantRole.Admin);

        return app;
    }

    /// <summary>
    /// Sin autenticación: la imagen la piden el visor público de documentos,
    /// el conversor de PDF y cualquier cliente de correo que pinte el HTML.
    /// El id es el hash del contenido: no se puede listar ni adivinar otro.
    /// </summary>
    public static IEndpointRouteBuilder MapTenantAssetPublicEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapMethods(
                PublicPrefix + "/{tenantId}/{file}",
                [HttpMethods.Get, HttpMethods.Head],
                DownloadAsync)
            .AllowAnonymous()
            .RequireRateLimiting(SharingEndpoints.RateLimitPolicy)
            .ExcludeFromDescription();

        return app;
    }

    /// <summary>
    /// El fichero se lee a mano del formulario y no con un parámetro
    /// IFormFile: así el endpoint no exige antiforgery, que en una API con
    /// clave no aplica.
    /// </summary>
    private static async Task<IResult> UploadLogoAsync(
        string tenantId,
        HttpRequest request,
        ITenantAssetRepository assets,
        TenantAssetOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
            return Error("assets.file_required", "Falta la imagen (multipart/form-data, campo \"file\").");

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

        if (file is null || file.Length == 0)
            return Error("assets.file_required", "Falta la imagen (multipart/form-data, campo \"file\").");

        if (file.Length > TenantAssetRules.MaxLogoBytes)
        {
            return Error(
                "assets.too_large",
                $"El logo no puede pasar de {TenantAssetRules.MaxLogoBytes / 1024} KB. Guárdalo más pequeño (con 400 px de ancho sobra).");
        }

        byte[] content;

        await using (var stream = file.OpenReadStream())
        {
            using var memory = new MemoryStream((int)file.Length);
            await stream.CopyToAsync(memory, cancellationToken);
            content = memory.ToArray();
        }

        var contentType = TenantAssetRules.DetectContentType(content);

        if (contentType is null)
        {
            return Error(
                "assets.type_not_supported",
                "El logo tiene que ser PNG, JPG, WebP o GIF. SVG no se admite.");
        }

        var baseUrl = options.BaseUrl();

        if (baseUrl is null)
        {
            return Results.Json(
                new[]
                {
                    new RenderValidationError
                    {
                        Code = "assets.public_url_missing",
                        Message = "Falta el dominio público de la API (Assets:PublicBaseUrl o DocumentSharing:PublicBaseUrl): sin él la imagen no tendría dirección.",
                        Path = "file"
                    }
                },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var caller = request.HttpContext.User.GetCaller();

        var saved =
            await assets.SaveAsync(
                new TenantAsset
                {
                    TenantId = tenantId,
                    AssetId = TenantAssetRules.IdFor(content),
                    ContentType = contentType,
                    FileName = Path.GetFileName(file.FileName),
                    SizeBytes = content.LongLength,
                    CreatedAtUtc = time.GetUtcNow().UtcDateTime,
                    CreatedBy = caller?.UserId ?? caller?.Name
                },
                content,
                cancellationToken);

        return Results.Ok(
            new TenantAssetResponse
            {
                Url = $"{baseUrl}{PublicPrefix}/{Uri.EscapeDataString(tenantId)}/{saved.PublicFileName}",
                AssetId = saved.AssetId,
                ContentType = saved.ContentType,
                SizeBytes = saved.SizeBytes
            });
    }

    private static async Task<IResult> DownloadAsync(
        string tenantId,
        string file,
        HttpContext http,
        ITenantAssetRepository assets,
        CancellationToken cancellationToken)
    {
        var headers = http.Response.Headers;

        headers.XContentTypeOptions = "nosniff";

        // Es una imagen: nada de lo que lleve se ejecuta aunque alguien
        // la abra como página.
        headers.ContentSecurityPolicy = "default-src 'none'; sandbox";

        var assetId = TenantAssetRules.ParsePublicFileName(file);

        if (assetId is null)
            return Results.NotFound();

        var found =
            await assets.GetAsync(
                tenantId,
                assetId,
                cancellationToken);

        if (found is not { } asset)
            return Results.NotFound();

        // El contenido de un id no cambia nunca.
        headers.CacheControl = "public, max-age=31536000, immutable";
        headers.ETag = $"\"{asset.Asset.AssetId}\"";

        // Pintada desde otros dominios (Web, el portal DeCA, el visor).
        headers["Cross-Origin-Resource-Policy"] = "cross-origin";

        return Results.Bytes(
            asset.Content,
            asset.Asset.ContentType);
    }

    private static IResult Error(
        string code,
        string message) =>
        Results.BadRequest(
            new[]
            {
                new RenderValidationError
                {
                    Code = code,
                    Message = message,
                    Path = "file"
                }
            });
}

/// <summary>
/// "Assets": { "PublicBaseUrl": "https://api.renderset.app" }. Vacío = el
/// de los enlaces compartidos. Tiene que llegar a la API: es la que sirve
/// /assets.
/// </summary>
public sealed class TenantAssetOptions
{
    public const string SectionName = "Assets";

    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Sólo el dominio, como en el QR del DeCA: la API sirve /assets en la
    /// raíz, así que una ruta en la configuración daría URLs rotas.
    /// </summary>
    public string? BaseUrl()
    {
        if (string.IsNullOrWhiteSpace(PublicBaseUrl) ||
            !Uri.TryCreate(PublicBaseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
