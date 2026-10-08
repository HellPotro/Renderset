using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Renderset.Core.Rendering;
using Renderset.Core.Security;
using Renderset.Core.Tenancy;

namespace Renderset.Api.Security;

/// <summary>
/// Autenticación por clave en la cabecera X-Api-Key (o Authorization:
/// Bearer &lt;clave&gt;). Es la que ya envía Renderset.Client.
///
/// Primero se mira si es una clave de servicio (configuración) y si no, una
/// API key de tenant (base de datos). El resultado es un usuario con dos
/// claims: el tipo de llamante y, si es de tenant, su tenant.
/// </summary>
public sealed class ApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "RendersetApiKey";

    public const string HeaderName = "X-Api-Key";

    private readonly IApiKeyService _keys;
    private readonly ServiceKeyRegistry _serviceKeys;
    private readonly UserTokenSettings _userTokens;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService keys,
        ServiceKeyRegistry serviceKeys,
        UserTokenSettings userTokens)
        : base(options, logger, encoder)
    {
        _keys = keys;
        _serviceKeys = serviceKeys;
        _userTokens = userTokens;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var raw = ReadKey(Request);

        if (string.IsNullOrWhiteSpace(raw))
            return AuthenticateResult.NoResult();

        var service =
            _serviceKeys.Match(
                ApiKeyFormat.Hash(raw));

        if (service is not null)
            return AuthenticateService(service);

        var key =
            await _keys.AuthenticateAsync(
                raw,
                Context.RequestAborted);

        if (key is null)
            return AuthenticateResult.Fail("Clave de API no válida.");

        return Success(
            RendersetCallerKind.Tenant,
            key.TenantId,
            $"{key.Name} ({key.Prefix}…)");
    }

    protected override async Task HandleChallengeAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "ApiKey header=\"X-Api-Key\"";

        await Response.WriteAsJsonAsync(
            new[]
            {
                new RenderValidationError
                {
                    Code = "auth.api_key_required",
                    Message = "Falta una API key válida en la cabecera X-Api-Key.",
                    Path = HeaderName
                }
            });
    }

    protected override async Task HandleForbiddenAsync(
        AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;

        await Response.WriteAsJsonAsync(
            new[]
            {
                new RenderValidationError
                {
                    Code = "auth.forbidden",
                    Message = "Esta clave no tiene permiso para esta operación.",
                    Path = HeaderName
                }
            });
    }

    /// <summary>
    /// Clave de servicio: con token de usuario queda limitada al tenant y al
    /// rol del token; sin él, sólo si la configuración lo permite.
    /// </summary>
    private AuthenticateResult AuthenticateService(
        string service)
    {
        var token = Request.Headers[TenantTokenFormat.HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(token))
        {
            return _userTokens.Required
                ? AuthenticateResult.Fail("La clave de servicio necesita el token de usuario.")
                : Success(RendersetCallerKind.Service, tenantId: null, name: service);
        }

        if (_userTokens.Validator is null)
            return AuthenticateResult.Fail("La API no tiene configurada la clave pública de los tokens de usuario.");

        if (!_userTokens.Validator.TryValidate(token, out var claims, out var error))
            return AuthenticateResult.Fail(error ?? "Token de usuario no válido.");

        return Success(
            RendersetCallerKind.Service,
            claims!.TenantId,
            $"{service} · {claims.UserId}",
            claims.UserId,
            claims.Role);
    }

    private AuthenticateResult Success(
        RendersetCallerKind kind,
        string? tenantId,
        string name,
        string? userId = null,
        TenantRole? role = null)
    {
        var claims =
            new List<Claim>
            {
                new(RendersetClaims.CallerKind, kind.ToString()),
                new(ClaimTypes.Name, name)
            };

        if (!string.IsNullOrWhiteSpace(tenantId))
            claims.Add(new Claim(RendersetClaims.TenantId, tenantId));

        if (!string.IsNullOrWhiteSpace(userId))
            claims.Add(new Claim(RendersetClaims.UserId, userId));

        if (role is not null)
            claims.Add(new Claim(RendersetClaims.Role, role.Value.ToString()));

        var principal =
            new ClaimsPrincipal(
                new ClaimsIdentity(claims, SchemeName));

        return AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName));
    }

    private static string? ReadKey(
        HttpRequest request)
    {
        var header = request.Headers[HeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(header))
            return header.Trim();

        var authorization = request.Headers.Authorization.ToString();

        const string bearer = "Bearer ";

        return authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase)
            ? authorization[bearer.Length..].Trim()
            : null;
    }
}


public static class RendersetClaims
{
    public const string CallerKind = "rs:caller_kind";

    public const string TenantId = "rs:tenant";

    public const string UserId = "rs:user";

    public const string Role = "rs:role";

    public static RendersetCaller? GetCaller(
        this ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return null;

        if (!Enum.TryParse<RendersetCallerKind>(
                user.FindFirst(CallerKind)?.Value,
                out var kind))
        {
            return null;
        }

        return new RendersetCaller
        {
            Kind = kind,
            TenantId = user.FindFirst(TenantId)?.Value,
            UserId = user.FindFirst(UserId)?.Value,
            Role = Enum.TryParse<TenantRole>(user.FindFirst(Role)?.Value, out var role)
                ? role
                : null,
            Name = user.Identity.Name ?? kind.ToString()
        };
    }
}


public static class RendersetPolicies
{
    /// <summary>Cualquier clave válida (de tenant o de servicio).</summary>
    public const string ApiCaller = "rs:api-caller";

    /// <summary>Sólo aplicaciones de confianza (gestión de claves).</summary>
    public const string ServiceOnly = "rs:service-only";
}



/// <summary>
/// Validador de tokens de usuario y si son obligatorios, resueltos una vez
/// al arrancar (ver Program.cs).
/// </summary>
public sealed class UserTokenSettings
{
    public UserTokenSettings(
        TenantTokenValidator? validator,
        bool required)
    {
        Validator = validator;
        Required = required;
    }

    public TenantTokenValidator? Validator { get; }

    public bool Required { get; }
}


/// <summary>
/// Operaciones de administración del tenant (claves, página pública,
/// idiomas): con usuario de Web, sólo Admin u Owner.
/// </summary>
public sealed class TenantRoleFilter(TenantRole required)
    : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var caller = context.HttpContext.User.GetCaller();

        if (caller is null || !caller.HasRole(required))
        {
            return Results.Json(
                new[]
                {
                    new RenderValidationError
                    {
                        Code = "auth.role_required",
                        Message = $"Hace falta el rol {required} en el tenant para esta operación.",
                        Path = TenantTokenFormat.HeaderName
                    }
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}


public static class TenantRoleFilterExtensions
{
    public static TBuilder RequireTenantRole<TBuilder>(
        this TBuilder builder,
        TenantRole role)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new TenantRoleFilter(role));
}
