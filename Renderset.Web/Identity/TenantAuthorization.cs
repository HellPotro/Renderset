using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Renderset.Core.Tenancy;

namespace Renderset.Web.Identity;

/// <summary>
/// Políticas de Web por rol en el tenant elegido. El rol se lee de
/// TenantMembers, no de la cookie.
///
///     [Authorize(Policy = TenantPolicies.Admin)]   miembros, API keys, página pública, idiomas
/// </summary>
public static class TenantPolicies
{
    public const string Member = "rs:tenant-member";

    public const string Admin = "rs:tenant-admin";

    public const string Owner = "rs:tenant-owner";
}


public sealed class TenantRoleRequirement(TenantRole role)
    : IAuthorizationRequirement
{
    public TenantRole Role { get; } = role;
}


public sealed class TenantRoleHandler(
    TenantDirectory directory)
    : AuthorizationHandler<TenantRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var tenantId = context.User.FindFirst(RendersetClaimTypes.TenantId)?.Value;

        var membership = await directory.GetMembershipAsync(userId, tenantId);

        if (membership is not null && membership.Role >= requirement.Role)
            context.Succeed(requirement);
    }
}


/// <summary>
/// Rol del usuario en el tenant actual, para enseñar u ocultar opciones en
/// las páginas (la protección de verdad son las políticas y la API).
/// </summary>
public sealed class TenantSession(
    UserCurrentTenant current,
    TenantDirectory directory)
{
    public UserCurrentTenant Current => current;

    public async Task<TenantMembership?> GetMembershipAsync(
        CancellationToken cancellationToken = default) =>
        current.IsAuthenticated
            ? await directory.GetMembershipAsync(
                current.UserId,
                current.User.FindFirst(RendersetClaimTypes.TenantId)?.Value,
                cancellationToken)
            : null;

    public async Task<IReadOnlyList<TenantMembership>> GetMembershipsAsync(
        CancellationToken cancellationToken = default) =>
        current.UserId is { } userId
            ? await directory.GetMembershipsAsync(userId, cancellationToken)
            : [];

    public async Task<TenantActor?> GetActorAsync(
        CancellationToken cancellationToken = default)
    {
        var membership = await GetMembershipAsync(cancellationToken);

        return membership is null || current.UserId is null
            ? null
            : new TenantActor(current.UserId, current.UserDisplayName ?? "RenderSet", membership.Role);
    }
}
