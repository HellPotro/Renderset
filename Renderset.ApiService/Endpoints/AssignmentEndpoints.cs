using Renderset.Core.Presets;

namespace Renderset.Api.Endpoints;

public static class AssignmentEndpoints
{
    public static IEndpointRouteBuilder MapAssignmentEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/assignments")
                .WithTags("Assignments");

        group.MapGet(
            "/{tenantId}/{reportId}",
            async (
                string tenantId,
                string reportId,
                IReportPresetAssignmentRepository repository,
                CancellationToken cancellationToken) =>
            {
                var assignments =
                    await repository.GetAllAsync(
                        tenantId,
                        reportId,
                        cancellationToken);

                return Results.Ok(assignments);
            });

        group.MapGet(
            "/{tenantId}/{reportId}/default",
            GetDefaultAsync);

        group.MapGet(
            "/{tenantId}/{reportId}/{contextType}/{contextKey}",
            GetAsync);

        group.MapPut(
            "/{tenantId}",
            SaveAsync);

        return app;
    }

    private static async Task<IResult> GetDefaultAsync(
        string tenantId,
        string reportId,
        IReportPresetAssignmentRepository repository,
        CancellationToken cancellationToken)
    {
        var assignment =
            await repository.GetDefaultAsync(
                tenantId,
                reportId,
                cancellationToken);

        return assignment is null
            ? Results.NotFound()
            : Results.Ok(assignment);
    }

    private static async Task<IResult> GetAsync(
        string tenantId,
        string reportId,
        string contextType,
        string contextKey,
        IReportPresetAssignmentRepository repository,
        CancellationToken cancellationToken)
    {
        var assignment =
            await repository.GetAsync(
                tenantId,
                reportId,
                contextType,
                contextKey,
                cancellationToken);

        return assignment is null
            ? Results.NotFound()
            : Results.Ok(assignment);
    }

    private static async Task<IResult> SaveAsync(
        string tenantId,
        ReportPresetAssignment assignment,
        IReportPresetAssignmentRepository repository,
        CancellationToken cancellationToken)
    {
        await repository.SaveAsync(
            tenantId,
            assignment,
            cancellationToken);

        return Results.NoContent();
    }
}