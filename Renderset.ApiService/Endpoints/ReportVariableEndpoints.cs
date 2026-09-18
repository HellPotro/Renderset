using Renderset.Core.Localization;
using Renderset.Core.Variables;

public static class ReportVariableEndpoints
{
    public static IEndpointRouteBuilder MapReportVariableEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/variables")
                .WithTags("Variables");

        group.MapGet(
            "/{tenantId}",
            async (
                string tenantId,
                IReportVariableRepository repository,
                CancellationToken cancellationToken) =>
            {
                var variables =
                    await repository.GetAllAsync(
                        tenantId,
                        cancellationToken);

                return Results.Ok(variables);
            });

        group.MapPut(
            "/{tenantId}/{key}",
            async (
                string tenantId,
                string key,
                ReportVariable variable,
                IReportVariableRepository repository,
                ITenantCultureRepository cultures,
                IReportResourceRepository resources,
                CancellationToken cancellationToken) =>
            {
                if (!string.Equals(
                        key,
                        variable.Key,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest();
                }

                await repository.SaveAsync(
                    tenantId,
                    variable,
                    cancellationToken);

                await SeedDictionaryAsync(
                    tenantId,
                    variable,
                    cultures,
                    resources,
                    cancellationToken);

                return Results.NoContent();
            });

        group.MapDelete(
            "/{tenantId}/{key}",
            async (
                string tenantId,
                string key,
                IReportVariableRepository repository,
                CancellationToken cancellationToken) =>
            {
                await repository.DeleteAsync(
                    tenantId,
                    key,
                    cancellationToken);

                return Results.NoContent();
            });

        return app;
    }


    /// <summary>
    /// Da de alta la clave de la variable en el diccionario común del tenant
    /// para que aparezca junto a los bloques y se pueda traducir.
    /// 
    /// Sólo siembra lo que falta: si ya hay traducciones, volver a guardar la
    /// variable no puede pisarlas. Y el valor literal se usa como semilla de
    /// la cultura base, que es lo que ya está escrito y por tanto la mejor
    /// referencia desde la que traducir.
    /// </summary>
    private static async Task SeedDictionaryAsync(
        string tenantId,
        ReportVariable variable,
        ITenantCultureRepository cultures,
        IReportResourceRepository resources,
        CancellationToken cancellationToken)
    {
        if (!variable.Translatable ||
            string.IsNullOrWhiteSpace(variable.Key))
        {
            return;
        }

        var tenantCultures =
            await cultures.GetAllAsync(
                tenantId,
                cancellationToken);

        if (tenantCultures.Count == 0)
            return;

        var baseCulture =
            tenantCultures.FirstOrDefault(x => x.IsDefault)
            ?? tenantCultures.First();

        var resourceKey = ReportTextKeys.Variable(variable.Key);

        var seeds =
            tenantCultures
                .Select(culture =>
                    new ReportResource
                    {
                        Scope = ReportResourceScope.Global,
                        Key = resourceKey,
                        Culture = culture.Culture,

                        // Las demás culturas entran vacías: así la clave
                        // aparece como pendiente en el diccionario en vez de
                        // quedarse invisible hasta que alguien la escriba.
                        Value =
                            string.Equals(
                                culture.Culture,
                                baseCulture.Culture,
                                StringComparison.OrdinalIgnoreCase)
                                ? variable.Value
                                : null,

                        Description = variable.Description,
                        Source = ReportResourceSource.Seed
                    })
                .ToList();

        await resources.SeedMissingAsync(
            tenantId,
            seeds,
            cancellationToken);
    }
}