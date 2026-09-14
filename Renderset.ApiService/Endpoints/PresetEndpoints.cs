using Renderset.Core.Persistence;
using Renderset.Core.Presets;

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

        return app;
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