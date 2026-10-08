using Renderset.Core.Persistence;
using Renderset.Core.Presets;
using Renderset.Core.Rendering;

namespace Renderset.Api.Endpoints;

public static class PresetEndpoints
{
    public static IEndpointRouteBuilder MapPresetEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/presets")
            .WithTags("Presets");

        group.MapGet(
            "/{tenantId}/{presetId}",
            GetPresetAsync);

        group.MapGet(
            "/{tenantId}",
            GetAllPresetsAsync);

        group.MapPut(
            "/{tenantId}/{presetId}",
            SavePresetAsync);

        group.MapGet(
            "/resolve/{tenantId}/{reportId}",
            ResolvePresetAsync);

        group.MapDelete(
            "/{tenantId}/{presetId}",
            DeletePresetAsync);

        return app;
    }

    /// <summary>
    /// Borra un preset y sus asignaciones. El preset por defecto no se puede
    /// borrar mientras el report tenga otros: el render y el diseñador se
    /// quedarían sin a quién recurrir. Si es el único, sí.
    /// </summary>
    private static async Task<IResult> DeletePresetAsync(
        string tenantId,
        string presetId,
        IReportPresetRepository presets,
        IReportPresetAssignmentRepository assignments,
        CancellationToken cancellationToken)
    {
        var preset =
            await presets.GetByIdAsync(
                tenantId,
                presetId,
                cancellationToken);

        if (preset is null)
            return Results.NotFound();

        var reportId = preset.Configuration.ReportId;

        var defaultAssignment =
            await assignments.GetDefaultAsync(
                tenantId,
                reportId,
                cancellationToken);

        if (defaultAssignment is not null &&
            string.Equals(defaultAssignment.PresetId, presetId, StringComparison.OrdinalIgnoreCase))
        {
            var all =
                await presets.GetAllAsync(
                    tenantId,
                    cancellationToken);

            var others =
                all.Count(x =>
                    string.Equals(x.Configuration.ReportId, reportId, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(x.Id, presetId, StringComparison.OrdinalIgnoreCase));

            if (others > 0)
            {
                return Results.Json(
                    new[]
                    {
                        new RenderValidationError
                        {
                            Code = "preset.is_default",
                            Message = "Es el preset por defecto del report. Marca otro como predeterminado antes de borrarlo.",
                            Path = "presetId"
                        }
                    },
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        await assignments.DeleteByPresetAsync(
            tenantId,
            presetId,
            cancellationToken);

        await presets.DeleteAsync(
            tenantId,
            presetId,
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetPresetAsync(
        string tenantId,
        string presetId,
        IReportPresetRepository repository,
        CancellationToken cancellationToken)
    {
        var preset =
            await repository.GetByIdAsync(
                tenantId,
                presetId,
                cancellationToken);

        return preset is null
            ? Results.NotFound()
            : Results.Ok(preset);
    }

    private static async Task<IResult> GetAllPresetsAsync(
        string tenantId,
        IReportPresetRepository repository,
        CancellationToken cancellationToken)
    {
        var presets =
            await repository.GetAllAsync(
                tenantId,
                cancellationToken);

        return Results.Ok(presets);
    }

    private static async Task<IResult> SavePresetAsync(
        string tenantId,
        string presetId,
        ReportPreset preset,
        IReportPresetRepository repository,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                presetId,
                preset.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(
                "El presetId de la URL no coincide con el Id del preset.");
        }

        await repository.SaveAsync(
            tenantId,
            preset,
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> ResolvePresetAsync(
    string tenantId,
    string reportId,
    string? contextType,
    string? contextKey,
    IReportPresetProvider provider,
    CancellationToken cancellationToken)
    {
        var preset =
            await provider.GetPresetAsync(
                tenantId,
                reportId,
                contextType,
                contextKey,
                cancellationToken);

        return preset is null
            ? Results.NotFound()
            : Results.Ok(preset);
    }
}