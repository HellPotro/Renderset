using Renderset.Core.Localization;
using Renderset.Core.Resources;
using Renderset.Core.Translations;

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

        group.MapPost(
            "/{tenantId}/copy-missing",
            async (
                string tenantId,
                CopyMissingReportResourcesRequest request,
                IReportResourceRepository repository,
                CancellationToken cancellationToken) =>
            {
                var result =
                await repository.CopyMissingAsync(
                    tenantId,
                    request,
                    cancellationToken);

                return Results.Ok(result);
            });

        group.MapPost(
            "/{tenantId}/translate-missing",
            TranslateMissingAsync);

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

        return Results.Ok(catalog.Entries);
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


    private static async Task<IResult> TranslateMissingAsync(
        string tenantId,
        TranslateMissingReportResourcesRequest request,
        IReportResourceRepository repository,
        ITranslationService translationService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Scope) ||
            string.IsNullOrWhiteSpace(request.SourceCulture) ||
            string.IsNullOrWhiteSpace(request.TargetCulture))
        {
            return Results.BadRequest(
                "Scope, SourceCulture y TargetCulture son obligatorios.");
        }

        if (string.Equals(
                request.SourceCulture,
                request.TargetCulture,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(
                "La cultura origen y la cultura destino no pueden ser iguales.");
        }

        var resources =
            await repository.GetByScopeAsync(
                tenantId,
                request.Scope,
                cancellationToken);

        var sourceResources =
            resources
                .Where(x =>
                    string.Equals(
                        x.Culture,
                        request.SourceCulture,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(x.Value))
                .OrderBy(x => x.Key)
                .ToList();

        var targetByKey =
            resources
                .Where(x =>
                    string.Equals(
                        x.Culture,
                        request.TargetCulture,
                        StringComparison.OrdinalIgnoreCase))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.First(),
                    StringComparer.OrdinalIgnoreCase);

        var itemsToTranslate =
            new List<TranslationItem>();

        var skipped =
            0;

        foreach (var source in sourceResources)
        {
            if (targetByKey.TryGetValue(source.Key, out var target))
            {
                var hasValue =
                    !string.IsNullOrWhiteSpace(target.Value);

                if (hasValue && !request.OverwriteExistingValues)
                {
                    skipped++;
                    continue;
                }

                if (!hasValue && !request.IncludeEmptyValues)
                {
                    skipped++;
                    continue;
                }
            }

            itemsToTranslate.Add(
                new TranslationItem
                {
                    Key = source.Key,
                    Text = source.Value!,
                    Description = source.Description,
                    Context = request.Scope
                });
        }

        if (itemsToTranslate.Count == 0)
        {
            return Results.Ok(
                new TranslateMissingReportResourcesResult
                {
                    Scope = request.Scope,
                    SourceCulture = request.SourceCulture,
                    TargetCulture = request.TargetCulture,
                    Provider = "AzureTranslator",
                    Translated = 0,
                    Skipped = skipped,
                    Failed = 0
                });
        }

        var translated =
            await translationService.TranslateAsync(
                new TranslationRequest
                {
                    SourceCulture = request.SourceCulture,
                    TargetCulture = request.TargetCulture,
                    Items = itemsToTranslate
                },
                cancellationToken);

        var sourceByKey =
            sourceResources.ToDictionary(
                x => x.Key,
                StringComparer.OrdinalIgnoreCase);

        var resourcesToSave =
            translated
                .Select(result =>
                {
                    sourceByKey.TryGetValue(
                        result.Key,
                        out var source);

                    return new ReportResource
                    {
                        Scope = request.Scope,
                        Key = result.Key,
                        Culture = request.TargetCulture,
                        Value = result.TranslatedText,
                        Source = ReportResourceSource.Machine,
                        Description = source?.Description
                    };
                })
                .ToList();

        await repository.SaveManyAsync(
            tenantId,
            resourcesToSave,
            cancellationToken);

        return Results.Ok(
            new TranslateMissingReportResourcesResult
            {
                Scope = request.Scope,
                SourceCulture = request.SourceCulture,
                TargetCulture = request.TargetCulture,
                Provider = translated.FirstOrDefault()?.Provider ?? "AzureTranslator",
                Translated = resourcesToSave.Count,
                Skipped = skipped,
                Failed = itemsToTranslate.Count - resourcesToSave.Count
            });
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
