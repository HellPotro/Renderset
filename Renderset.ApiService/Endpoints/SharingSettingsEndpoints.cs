using Renderset.Core.Sharing;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Configuración de la página pública de cada tenant.
///
///     GET  /api/sharing/{tenantId}/settings
///     PUT  /api/sharing/{tenantId}/settings
///     POST /api/sharing/{tenantId}/preview      HTML con la configuración sin guardar
/// </summary>
public static class SharingSettingsEndpoints
{
    public static IEndpointRouteBuilder MapSharingSettingsEndpoints(
        this IEndpointRouteBuilder app)
    {
        var sharing =
            app.MapGroup("/api/sharing")
                .WithTags("Sharing settings");

        sharing.MapGet(
            "/{tenantId}/settings",
            GetAsync);

        sharing.MapPut(
            "/{tenantId}/settings",
            SaveAsync);

        sharing.MapPost(
            "/{tenantId}/preview",
            PreviewAsync);

        return app;
    }

    private static async Task<IResult> GetAsync(
        string tenantId,
        IDocumentSharingSettingsRepository repository,
        DocumentSharingOptions options,
        CancellationToken cancellationToken)
    {
        var settings =
            await repository.GetAsync(
                tenantId,
                cancellationToken);

        if (settings is null)
            return Results.NotFound();

        return Results.Ok(
            new DocumentSharingSettingsResponse
            {
                Settings = settings,
                MaxExpirationDays = options.MaxExpirationDays,
                SystemDefaultExpirationDays = options.DefaultExpirationDays
            });
    }

    private static async Task<IResult> SaveAsync(
        string tenantId,
        DocumentSharingSettings settings,
        IDocumentSharingSettingsRepository repository,
        DocumentSharingOptions options,
        CancellationToken cancellationToken)
    {
        var errors =
            settings.Validate(options.MaxExpirationDays);

        if (errors.Count > 0)
            return Results.BadRequest(errors);

        var saved =
            await repository.SaveAsync(
                tenantId,
                settings,
                cancellationToken);

        return saved
            ? Results.NoContent()
            : Results.NotFound();
    }

    /// <summary>
    /// Mismo renderer que el enlace real, con documentos de ejemplo. Los
    /// valores que no pasan la validación se ignoran igual que al pintar la
    /// página de verdad, así que lo que se ve es lo que vería el cliente.
    /// </summary>
    private static async Task<IResult> PreviewAsync(
        string tenantId,
        DocumentSharingPreviewRequest request,
        IDocumentSharingSettingsRepository repository,
        IDocumentBundlePageRenderer pages,
        DocumentSharingOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // El nombre del tenant no viaja en la petición: se toma del guardado.
        var stored =
            await repository.GetAsync(
                tenantId,
                cancellationToken);

        if (stored is null)
            return Results.NotFound();

        var settings = request.Settings ?? new DocumentSharingSettings();
        settings.TenantName = stored.TenantName;

        string html;

        if (request.Expired)
        {
            var texts = DocumentBundleTexts.For(request.Culture);

            html =
                await pages.RenderUnavailableAsync(
                    new DocumentBundleUnavailableView
                    {
                        Reason = DocumentBundleOpenStatus.Expired,
                        FooterText = settings.Normalized().FooterText,
                        Culture = texts.Language,
                        Branding = settings.ToBranding(tenantId),
                        Texts = texts
                    },
                    cancellationToken);
        }
        else
        {
            html =
                await pages.RenderViewerAsync(
                    DocumentBundlePreview.Build(
                        tenantId,
                        settings,
                        request.Culture,
                        time.GetUtcNow().UtcDateTime,
                        options.DefaultExpirationDays),
                    cancellationToken);
        }

        return Results.Content(
            html,
            "text/html; charset=utf-8");
    }
}
