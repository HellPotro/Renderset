using Renderset.Core.Rendering;

namespace Renderset.Api.Security;

/// <summary>
/// Comprueba que el tenant de la ruta es el de la clave.
///
/// Se aplica a todo /api en un solo sitio, en vez de en cada endpoint: un
/// endpoint nuevo que se olvidara de comprobarlo dejaría a un cliente leer
/// los documentos de otro con sólo cambiar la URL.
/// </summary>
public sealed class TenantAccessFilter
    : IEndpointFilter
{
    public const string RouteKey = "tenantId";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var caller = http.User.GetCaller();

        // Sin llamante ya lo habría parado la autorización; esto cubre un
        // endpoint mal configurado como anónimo dentro del grupo.
        if (caller is null)
            return Results.Unauthorized();

        var routeTenant =
            http.Request.RouteValues.TryGetValue(RouteKey, out var value)
                ? value?.ToString()
                : null;

        // Rutas sin tenant: sólo servicios. Hoy no hay ninguna, pero si
        // aparece una, por defecto queda cerrada a las claves de cliente.
        if (routeTenant is null && !caller.IsService)
            return Forbidden("auth.tenant_required", "Esta operación no está disponible con una clave de tenant.");

        if (routeTenant is not null && !caller.CanAccessTenant(routeTenant))
        {
            return Forbidden(
                "auth.tenant_mismatch",
                $"La clave es del tenant '{caller.TenantId}' y la ruta pide '{routeTenant}'.");
        }

        return await next(context);
    }

    private static IResult Forbidden(
        string code,
        string message) =>
        Results.Json(
            new[]
            {
                new RenderValidationError
                {
                    Code = code,
                    Message = message,
                    Path = RouteKey
                }
            },
            statusCode: StatusCodes.Status403Forbidden);
}
