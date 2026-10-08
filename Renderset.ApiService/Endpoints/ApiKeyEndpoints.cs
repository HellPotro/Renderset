using Renderset.Core.Tenancy;
using Renderset.Api.Security;
using Renderset.Core.Rendering;
using Renderset.Core.Security;

namespace Renderset.Api.Endpoints;

/// <summary>
/// Gestión de las API keys de un tenant. Sólo para servicios de confianza
/// (RenderSet Web): una clave de tenant no puede crear otras claves.
///
///     GET  /api/keys/{tenantId}
///     POST /api/keys/{tenantId}                    { name, expiresInDays? } → clave en claro, una vez
///     POST /api/keys/{tenantId}/{keyId}/revoke
/// </summary>
public static class ApiKeyEndpoints
{
    public const int MaxExpirationDays = 3650;

    public static IEndpointRouteBuilder MapApiKeyEndpoints(
        this IEndpointRouteBuilder app)
    {
        var keys =
            app.MapGroup("/api/keys")
                .WithTags("API keys")
                .RequireAuthorization(RendersetPolicies.ServiceOnly)
                .RequireTenantRole(TenantRole.Admin);

        keys.MapGet(
            "/{tenantId}",
            ListAsync);

        keys.MapPost(
            "/{tenantId}",
            CreateAsync);

        keys.MapPost(
            "/{tenantId}/{keyId:guid}/revoke",
            RevokeAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        string tenantId,
        IApiKeyService keys,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;

        var items =
            await keys.ListAsync(
                tenantId,
                cancellationToken);

        return Results.Ok(
            items
                .Select(x => ApiKeyResponse.From(x, now))
                .ToList());
    }

    private static async Task<IResult> CreateAsync(
        string tenantId,
        CreateApiKeyRequest request,
        IApiKeyService keys,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var errors = new List<RenderValidationError>();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors.Add(new RenderValidationError
            {
                Code = "keys.name_required",
                Message = "Pon un nombre que diga para qué es la clave: \"ERP producción\".",
                Path = "name"
            });
        }
        else if (request.Name.Trim().Length > ApiKeyService.MaxNameLength)
        {
            errors.Add(new RenderValidationError
            {
                Code = "keys.name_too_long",
                Message = $"El nombre no puede pasar de {ApiKeyService.MaxNameLength} caracteres.",
                Path = "name"
            });
        }

        if (request.ExpiresInDays is { } days &&
            (days < 1 || days > MaxExpirationDays))
        {
            errors.Add(new RenderValidationError
            {
                Code = "keys.expiration_out_of_range",
                Message = $"La caducidad tiene que estar entre 1 y {MaxExpirationDays} días.",
                Path = "expiresInDays"
            });
        }

        if (errors.Count > 0)
            return Results.BadRequest(errors);

        var now = time.GetUtcNow().UtcDateTime;

        var created =
            await keys.CreateAsync(
                tenantId,
                request.Name!,
                request.ExpiresInDays is { } expires
                    ? now.AddDays(expires)
                    : null,
                cancellationToken);

        return Results.Ok(
            new CreatedApiKeyResponse
            {
                Key = ApiKeyResponse.From(created.Key, now),
                PlainText = created.PlainText
            });
    }

    private static async Task<IResult> RevokeAsync(
        string tenantId,
        Guid keyId,
        IApiKeyService keys,
        CancellationToken cancellationToken)
    {
        var revoked =
            await keys.RevokeAsync(
                tenantId,
                keyId,
                cancellationToken);

        return revoked
            ? Results.NoContent()
            : Results.NotFound();
    }
}
