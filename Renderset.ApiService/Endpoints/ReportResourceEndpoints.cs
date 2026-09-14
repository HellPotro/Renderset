using Renderset.Core.Localization;

namespace Renderset.Api.Endpoints;

public static class ReportResourceEndpoints
{
    public static IEndpointRouteBuilder MapReportResourceEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/resources")
                .WithTags("Resources");

        group.MapGet(
            "/{tenantId}/coverage",
            GetCoverageAsync);

        group.MapGet(
            "/{tenantId}/{scope}",
            GetByScopeAsync);

        group.MapGet(
            "/{tenantId}/{scope}/catalog",
            GetCatalogAsync);

        group.MapPut(
            "/{tenantId}",
            SaveManyAsync);

        group.MapDelete(
            "/{tenantId}/{scope}/{key}",
            DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetByScopeAsync(
        string tenantId,
        string scope,
        IReportResourceRepository repository,
        CancellationToken cancellationToken)
    {
        var resources =
            await repository.GetByScopeAsync(
                tenantId,
                scope,
                cancellationToken);

        return Results.Ok(resources);
    }

    /// <summary>
    /// Devuelve el diccionario ya aplanado para una cultura concreta. Es lo
    /// que consume el preview del diseñador para pintar en otro idioma.
    /// </summary>
    private static async Task<IResult> GetCatalogAsync(
        string tenantId,
        string scope,
        string? presetId,
        string? culture,
        IReportTextCatalogFactory factory,
        CancellationToken cancellationToken)
    {
        var catalog =
            await factory.CreateAsync(
                tenantId,
                scope,
                presetId,
                culture,
                cancellationToken);

        return Results.Ok(
            new
            {
                culture = catalog.Culture,
                count = catalog.Count
            });
    }

    private static async Task<IResult> GetCoverageAsync(
        string tenantId,
        IReportResourceRepository repository,
        CancellationToken cancellationToken)
    {
        var coverage =
            await repository.GetCoverageAsync(
                tenantId,
                cancellationToken);

        return Results.Ok(coverage);
    }

    private static async Task<IResult> SaveManyAsync(
        string tenantId,
        ReportResource[] resources,
        IReportResourceRepository repository,
        CancellationToken cancellationToken)
    {
        if (resources.Length == 0)
            return Results.NoContent();

        var invalid = resources.FirstOrDefault(x =>
            string.IsNullOrWhiteSpace(x.Scope) ||
            string.IsNullOrWhiteSpace(x.Key) ||
            string.IsNullOrWhiteSpace(x.Culture));

        if (invalid is not null)
        {
            return Results.BadRequest(
                "Scope, Key y Culture son obligatorios en todos los recursos.");
        }

        await repository.SaveManyAsync(
            tenantId,
            resources,
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        string tenantId,
        string scope,
        string key,
        IReportResourceRepository repository,
        CancellationToken cancellationToken)
    {
        await repository.DeleteAsync(
            tenantId,
            scope,
            key,
            cancellationToken);

        return Results.NoContent();
    }
}
