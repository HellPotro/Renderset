using Renderset.Core.Localization;
using Renderset.Core.Reports;

namespace Renderset.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/reports")
                .WithTags("Reports");

        group.MapGet(
            "/{tenantId}",
            GetAllAsync);

        group.MapGet(
            "/{tenantId}/{reportId}",
            GetByIdAsync);

        group.MapPut(
            "/{tenantId}/{reportId}",
            SaveAsync);

        group.MapDelete(
            "/{tenantId}/{reportId}",
            DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        string tenantId,
        IReportRepository repository,
        CancellationToken cancellationToken)
    {
        var reports =
            await repository.GetAllAsync(
                tenantId,
                cancellationToken);

        return Results.Ok(reports);
    }

    private static async Task<IResult> GetByIdAsync(
        string tenantId,
        string reportId,
        IReportRepository repository,
        CancellationToken cancellationToken)
    {
        var report =
            await repository.GetByIdAsync(
                tenantId,
                reportId,
                cancellationToken);

        return report is null
            ? Results.NotFound()
            : Results.Ok(report);
    }

    private static async Task<IResult> SaveAsync(
        string tenantId,
        string reportId,
        Report report,
        IReportRepository repository,
        ITenantCultureRepository cultures,
        IReportResourceRepository resources,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                reportId,
                report.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(
                "El reportId de la URL no coincide con Report.Id.");
        }

        if (!string.Equals(
                report.Id,
                report.Definition.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(
                "Report.Id debe coincidir con ReportDefinition.Id.");
        }

        await repository.SaveAsync(
            tenantId,
            report,
            cancellationToken);

        await SeedDictionaryAsync(
            tenantId,
            report,
            cultures,
            resources,
            cancellationToken);

        return Results.NoContent();
    }


    /// <summary>
    /// Siembra el diccionario con las claves de la definición: el idioma base
    /// con los labels inferidos y el resto de idiomas como pendientes.
    ///
    /// Se hace aquí y no en la página para que valga igual cuando el report
    /// se crea desde la API. Y se ejecuta también al actualizar, con
    /// semántica de "sólo rellenar huecos": si mañana añades un campo a la
    /// definición, su clave aparece sola y no se pisa ninguna traducción.
    /// </summary>
    private static async Task SeedDictionaryAsync(
        string tenantId,
        Report report,
        ITenantCultureRepository cultures,
        IReportResourceRepository resources,
        CancellationToken cancellationToken)
    {
        var tenantCultures =
            await cultures.GetAllAsync(
                tenantId,
                cancellationToken);

        if (tenantCultures.Count == 0)
            return;

        var baseCulture =
            tenantCultures.FirstOrDefault(x => x.IsDefault)
            ?? tenantCultures.First();

        var seeds =
            ReportResourceSeeder.Build(
                report.Definition,
                baseCulture.Culture,
                tenantCultures
                    .Select(x => x.Culture)
                    .ToList());

        if (seeds.Count == 0)
            return;

        await resources.SeedMissingAsync(
            tenantId,
            seeds,
            cancellationToken);
    }

    private static async Task<IResult> DeleteAsync(
        string tenantId,
        string reportId,
        IReportRepository repository,
        CancellationToken cancellationToken)
    {
        await repository.DeleteAsync(
            tenantId,
            reportId,
            cancellationToken);

        return Results.NoContent();
    }
}