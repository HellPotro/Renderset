using Renderset.Api.Security;
using Renderset.Core.Tenancy;
using Renderset.Core.Localization;

namespace Renderset.Api.Endpoints;

public static class TenantCultureEndpoints
{
    public static IEndpointRouteBuilder MapTenantCultureEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group =
            app.MapGroup("/api/cultures")
                .WithTags("Cultures");

        group.MapGet(
            "/{tenantId}",
            GetAllAsync);

        // Cambiar los idiomas del tenant afecta a todos sus documentos.
        group.MapPut(
            "/{tenantId}/{culture}",
            SaveAsync)
            .RequireTenantRole(TenantRole.Admin);

        group.MapDelete(
            "/{tenantId}/{culture}",
            DeleteAsync)
            .RequireTenantRole(TenantRole.Admin);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        string tenantId,
        ITenantCultureRepository repository,
        CancellationToken cancellationToken)
    {
        var cultures =
            await repository.GetAllAsync(
                tenantId,
                cancellationToken);

        return Results.Ok(cultures);
    }

    private static async Task<IResult> SaveAsync(
        string tenantId,
        string culture,
        TenantCulture body,
        ITenantCultureRepository repository,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                culture,
                body.Culture,
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(
                "La cultura de la URL no coincide con la del cuerpo.");
        }

        await repository.SaveAsync(
            tenantId,
            body,
            cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        string tenantId,
        string culture,
        ITenantCultureRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.DeleteAsync(
                tenantId,
                culture,
                cancellationToken);

            return Results.NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }
}
