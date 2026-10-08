using Renderset.Core.Presets;
using Renderset.Core.Rendering;

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

        group.MapPut(
            "/{tenantId}/batch",
            SaveManyAsync);

        group.MapDelete(
            "/{tenantId}/id/{assignmentId:long}",
            DeleteAsync);

        return app;
    }

    /// <summary>
    /// Alta en bloque (importación de CSV). Todas o ninguna: si alguna fila
    /// apunta a un preset que no existe o es de otro report, no se guarda
    /// nada y se dice cuáles.
    /// </summary>
    private static async Task<IResult> SaveManyAsync(
        string tenantId,
        List<ReportPresetAssignment> assignments,
        IReportPresetAssignmentRepository repository,
        IReportPresetRepository presets,
        CancellationToken cancellationToken)
    {
        if (assignments is null || assignments.Count == 0)
            return Results.Ok(new { saved = 0 });

        if (assignments.Count > 5000)
        {
            return Results.BadRequest(
                "Como mucho 5000 asignaciones por petición.");
        }

        var all =
            await presets.GetAllAsync(
                tenantId,
                cancellationToken);

        var errors = new List<RenderValidationError>();

        for (var i = 0; i < assignments.Count; i++)
        {
            var assignment = assignments[i];

            if (string.IsNullOrWhiteSpace(assignment.ContextType) ||
                string.IsNullOrWhiteSpace(assignment.ContextKey))
            {
                errors.Add(Error(i, "assignment.context_required", "Falta el tipo de contexto o el valor."));
                continue;
            }

            var preset =
                all.FirstOrDefault(x =>
                    string.Equals(x.Id, assignment.PresetId, StringComparison.OrdinalIgnoreCase));

            if (preset is null)
            {
                errors.Add(Error(i, "preset.not_found", $"No existe el preset '{assignment.PresetId}'."));
                continue;
            }

            if (!string.Equals(preset.Configuration.ReportId, assignment.ReportId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(Error(i, "preset.report_mismatch", $"El preset '{preset.Id}' es del report '{preset.Configuration.ReportId}'."));
            }

            assignment.ContextType = assignment.ContextType.Trim();
            assignment.ContextKey = assignment.ContextKey.Trim();
        }

        if (errors.Count > 0)
            return Results.BadRequest(errors);

        var saved =
            await repository.SaveManyAsync(
                tenantId,
                assignments,
                cancellationToken);

        return Results.Ok(new { saved });
    }

    private static RenderValidationError Error(
        int index,
        string code,
        string message) =>
        new()
        {
            Code = code,
            Message = message,
            Path = $"[{index}]"
        };

    private static async Task<IResult> DeleteAsync(
        string tenantId,
        long assignmentId,
        IReportPresetAssignmentRepository repository,
        CancellationToken cancellationToken)
    {
        var deleted =
            await repository.DeleteAsync(
                tenantId,
                assignmentId,
                cancellationToken);

        return deleted
            ? Results.NoContent()
            : Results.NotFound();
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