using Renderset.Core.Paging;
using Renderset.Core.Rendering;
using Renderset.Core.Sharing;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Gestión de bundles: crear, consultar, revocar y renovar el enlace.
///
///     POST /api/bundles/{tenantId}                         crea y devuelve el enlace
///     GET  /api/bundles/{tenantId}?search=&status=&fromUtc=&toUtc=&take=&cursor=
///                                                          listado paginado por cursor
///     GET  /api/bundles/{tenantId}/{bundleId}              estado, accesos y enlace
///     POST /api/bundles/{tenantId}/{bundleId}/revoke       desactiva el enlace
///     POST /api/bundles/{tenantId}/{bundleId}/link         enlace nuevo, el viejo deja de valer
/// </summary>
public static class BundleEndpoints
{
    public static IEndpointRouteBuilder MapBundleEndpoints(
        this IEndpointRouteBuilder app)
    {
        var bundles =
            app.MapGroup("/api/bundles")
                .WithTags("Bundles");

        bundles.MapPost(
            "/{tenantId}",
            CreateAsync);

        bundles.MapGet(
            "/{tenantId}",
            ListAsync);

        bundles.MapGet(
            "/{tenantId}/{bundleId:guid}",
            GetAsync);

        bundles.MapPost(
            "/{tenantId}/{bundleId:guid}/revoke",
            RevokeAsync);

        bundles.MapPost(
            "/{tenantId}/{bundleId:guid}/link",
            RenewLinkAsync);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        string tenantId,
        CreateDocumentBundleRequest request,
        HttpContext httpContext,
        IDocumentBundleService bundles,
        DocumentSharingOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var result =
            await bundles.CreateAsync(
                tenantId,
                request,
                cancellationToken);

        if (!result.Succeeded)
            return Results.BadRequest(result.Errors);

        var bundle = result.Bundle!;

        var url =
            ShareLinks.PublicBundleUrl(
                httpContext,
                options,
                bundle.Id,
                result.Token!);

        return Results.Created(
            $"/api/bundles/{Uri.EscapeDataString(tenantId)}/{bundle.Id:D}",
            DocumentBundleResponse.From(
                bundle,
                time.GetUtcNow().UtcDateTime,
                url));
    }

    private static async Task<IResult> ListAsync(
        string tenantId,
        string? search,
        DocumentBundleStatus? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        string? cursor,
        HttpContext httpContext,
        IDocumentBundleRepository repository,
        IDocumentBundleService bundles,
        DocumentSharingOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        KeysetCursor? decoded = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            decoded = KeysetCursor.TryDecode(cursor);

            if (decoded is null)
                return InvalidCursor();
        }

        var now = time.GetUtcNow().UtcDateTime;

        var page =
            await repository.SearchAsync(
                tenantId,
                new DocumentBundleQuery
                {
                    Search = search,
                    Status = status,
                    FromUtc = fromUtc?.UtcDateTime,
                    ToUtc = toUtc?.UtcDateTime,
                    Take = take ?? 50,
                    Cursor = decoded,
                    NowUtc = now
                },
                cancellationToken);

        return Results.Ok(
            new PagedResult<DocumentBundleResponse>
            {
                Items = page.Items
                    .Select(x => ToResponse(x, now, httpContext, bundles, options))
                    .ToList(),
                NextCursor = page.NextCursor
            });
    }

    private static async Task<IResult> GetAsync(
        string tenantId,
        Guid bundleId,
        HttpContext httpContext,
        IDocumentBundleRepository repository,
        IDocumentBundleService bundles,
        DocumentSharingOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var bundle =
            await repository.GetAsync(
                tenantId,
                bundleId,
                cancellationToken);

        return bundle is null
            ? Results.NotFound()
            : Results.Ok(
                ToResponse(
                    bundle,
                    time.GetUtcNow().UtcDateTime,
                    httpContext,
                    bundles,
                    options));
    }

    /// <summary>
    /// Respuesta con el enlace recuperado del token cifrado. Si no se puede
    /// recuperar (bundle antiguo, claves perdidas), Url va nula y la
    /// pantalla ofrece generar uno nuevo.
    /// </summary>
    private static DocumentBundleResponse ToResponse(
        DocumentBundle bundle,
        DateTime nowUtc,
        HttpContext httpContext,
        IDocumentBundleService bundles,
        DocumentSharingOptions options)
    {
        var token = bundles.RecoverToken(bundle);

        return DocumentBundleResponse.From(
            bundle,
            nowUtc,
            token is null
                ? null
                : ShareLinks.PublicBundleUrl(
                    httpContext,
                    options,
                    bundle.Id,
                    token));
    }

    internal static IResult InvalidCursor() =>
        Results.BadRequest(
            new[]
            {
                new RenderValidationError
                {
                    Code = "paging.invalid_cursor",
                    Message = "El cursor no es válido. Vuelve a pedir la primera página.",
                    Path = "cursor"
                }
            });

    private static async Task<IResult> RevokeAsync(
        string tenantId,
        Guid bundleId,
        IDocumentBundleService bundles,
        CancellationToken cancellationToken)
    {
        var revoked =
            await bundles.RevokeAsync(
                tenantId,
                bundleId,
                cancellationToken);

        return revoked
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static async Task<IResult> RenewLinkAsync(
        string tenantId,
        Guid bundleId,
        RenewDocumentBundleLinkRequest? request,
        HttpContext httpContext,
        IDocumentBundleService bundles,
        DocumentSharingOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var result =
            await bundles.RenewLinkAsync(
                tenantId,
                bundleId,
                request ?? new RenewDocumentBundleLinkRequest(),
                cancellationToken);

        if (result.NotFound)
            return Results.NotFound();

        if (!result.Succeeded)
            return Results.BadRequest(result.Errors);

        var bundle = result.Bundle!;

        return Results.Ok(
            DocumentBundleResponse.From(
                bundle,
                time.GetUtcNow().UtcDateTime,
                ShareLinks.PublicBundleUrl(
                    httpContext,
                    options,
                    bundle.Id,
                    result.Token!)));
    }
}
