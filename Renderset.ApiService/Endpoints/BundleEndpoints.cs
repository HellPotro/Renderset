using Renderset.Core.Sharing;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Gestión de bundles: crear, consultar, revocar y renovar el enlace.
///
///     POST /api/bundles/{tenantId}                         crea y devuelve el enlace
///     GET  /api/bundles/{tenantId}?take=50                 últimos bundles
///     GET  /api/bundles/{tenantId}/{bundleId}              estado y accesos
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
        int? take,
        IDocumentBundleRepository repository,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var items =
            await repository.ListAsync(
                tenantId,
                take ?? 50,
                cancellationToken);

        var now = time.GetUtcNow().UtcDateTime;

        return Results.Ok(
            items
                .Select(x => DocumentBundleResponse.From(x, now))
                .ToList());
    }

    private static async Task<IResult> GetAsync(
        string tenantId,
        Guid bundleId,
        IDocumentBundleRepository repository,
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
                DocumentBundleResponse.From(
                    bundle,
                    time.GetUtcNow().UtcDateTime));
    }

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
