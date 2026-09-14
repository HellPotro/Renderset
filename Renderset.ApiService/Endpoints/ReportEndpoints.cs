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

        return Results.NoContent();
    }
}