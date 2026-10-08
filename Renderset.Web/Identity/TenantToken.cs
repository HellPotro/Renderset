using System.Security.Claims;
using Renderset.Core.Security;

namespace Renderset.Web.Identity;

/// <summary>
/// Token firmado para cada llamada de Web a la API: usuario, tenant y rol
/// sacados de TenantMembers en el momento, no de la cookie. Con él la API
/// limita la clave de servicio a ese tenant (ver TenantTokenFormat).
/// </summary>
public sealed class TenantTokenFactory(
    UserCurrentTenant current,
    TenantDirectory directory,
    TenantTokenSigner? signer = null)
{
    public bool Enabled =>
        signer is not null;

    public async Task<string?> CreateAsync(
        CancellationToken cancellationToken = default)
    {
        if (signer is null)
            return null;

        var userId = current.UserId;
        var tenantId = current.User.FindFirstValue(RendersetClaimTypes.TenantId);

        var membership =
            await directory.GetMembershipAsync(userId, tenantId, cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "La sesión ya no tiene acceso a este tenant. Vuelve a entrar.");

        return signer.Create(membership.TenantId, userId!, membership.Role);
    }
}


/// <summary>
/// Añade el token a cada petición. Se crea por circuito (ver Program.cs):
/// los handlers de IHttpClientFactory viven en otro ámbito y no verían al
/// usuario.
/// </summary>
public sealed class TenantTokenHandler(
    TenantTokenFactory tokens)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokens.CreateAsync(cancellationToken);

        request.Headers.Remove(TenantTokenFormat.HeaderName);

        if (token is not null)
            request.Headers.Add(TenantTokenFormat.HeaderName, token);

        return await base.SendAsync(request, cancellationToken);
    }
}
