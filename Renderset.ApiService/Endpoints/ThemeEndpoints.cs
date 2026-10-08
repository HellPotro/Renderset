using Renderset.Core.Presets;
using Renderset.Core.Themes;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Temas de empresa del tenant.
///
///     GET    /api/themes/{tenantId}              lista, con cuántos presets usan cada uno
///     GET    /api/themes/{tenantId}/{themeId}
///     PUT    /api/themes/{tenantId}/{themeId}    crea o actualiza
///     DELETE /api/themes/{tenantId}/{themeId}    baja lógica
/// </summary>
public static class ThemeEndpoints
{
    public static IEndpointRouteBuilder MapThemeEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/themes")
                .WithTags("Themes");

        group.MapGet(
            "/{tenantId}",
            GetAllAsync);

        group.MapGet(
            "/{tenantId}/{themeId}",
            GetAsync);

        group.MapPut(
            "/{tenantId}/{themeId}",
            SaveAsync);

        group.MapDelete(
            "/{tenantId}/{themeId}",
            DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        string tenantId,
        IReportThemeRepository themes,
        IReportPresetRepository presets,
        CancellationToken cancellationToken)
    {
        var all =
            await themes.GetAllAsync(
                tenantId,
                cancellationToken);

        // Para avisar antes de borrar o de cambiar un tema muy usado. Los
        // presets de un tenant son decenas, no miles: basta con contarlos.
        var usage =
            (await presets.GetAllAsync(
                tenantId,
                cancellationToken))
            .Select(x => x.Configuration.ThemeId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.Count(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var theme in all)
        {
            theme.PresetCount =
                usage.GetValueOrDefault(theme.Theme.Id);
        }

        return Results.Ok(all);
    }

    private static async Task<IResult> GetAsync(
        string tenantId,
        string themeId,
        IReportThemeRepository themes,
        CancellationToken cancellationToken)
    {
        var theme =
            await themes.GetByIdAsync(
                tenantId,
                themeId,
                cancellationToken);

        return theme is null
            ? Results.NotFound()
            : Results.Ok(theme);
    }

    private static async Task<IResult> SaveAsync(
        string tenantId,
        string themeId,
        TenantReportTheme request,
        IReportThemeRepository themes,
        CancellationToken cancellationToken)
    {
        if (request?.Theme is null)
            return Results.BadRequest(new[] { "Falta el tema." });

        if (!string.Equals(
                request.Theme.Id?.Trim(),
                themeId,
                StringComparison.Ordinal))
        {
            return Results.BadRequest(
                new[] { "El identificador del tema no coincide con el de la ruta." });
        }

        var errors =
            ReportThemeRules.Validate(request.Theme);

        if (errors.Count > 0)
            return Results.BadRequest(errors);

        await themes.SaveAsync(
            tenantId,
            new TenantReportTheme
            {
                Theme = ReportThemeRules.Normalize(request.Theme),
                IsDefault = request.IsDefault
            },
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        string tenantId,
        string themeId,
        IReportThemeRepository themes,
        CancellationToken cancellationToken)
    {
        var deleted =
            await themes.DeleteAsync(
                tenantId,
                themeId,
                cancellationToken);

        return deleted
            ? Results.NoContent()
            : Results.NotFound();
    }
}
