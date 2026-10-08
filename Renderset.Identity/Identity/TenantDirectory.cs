using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Renderset.Core.Tenancy;
using Renderset.Web.Email;

namespace Renderset.Web.Identity;

/// <summary>
/// Quién pertenece a cada tenant y con qué rol, e invitaciones.
///
/// Es la fuente de verdad de Web para el tenant de cada usuario: la cookie
/// sólo dice qué tenant ha elegido, y aquí se comprueba que sigue siendo
/// miembro (con una caché corta, para no ir a la base de datos en cada
/// llamada a la API).
/// </summary>
public sealed class TenantDirectory(
    IDbContextFactory<IdentityDb> contexts,
    UserManager<RendersetUser> users,
    IMemoryCache cache,
    IAppEmailSender email,
    TimeProvider time,
    ILogger<TenantDirectory> logger)
{
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private static readonly TimeSpan MembershipCache = TimeSpan.FromSeconds(30);

    // ------------------------------------------------------------ pertenencia

    public async Task<IReadOnlyList<TenantMembership>> GetMembershipsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        return await db.TenantMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Tenant.Active)
            .OrderBy(x => x.Tenant.Name)
            .Select(x => new TenantMembership(x.TenantId, x.Tenant.Name, x.Role))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Pertenencia de un usuario a un tenant, o nulo si no es miembro (o el
    /// tenant está desactivado). Cacheada unos segundos.
    /// </summary>
    public async Task<TenantMembership?> GetMembershipAsync(
        string? userId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tenantId))
            return null;

        var key = CacheKey(userId, tenantId);

        if (cache.TryGetValue(key, out TenantMembership? cached))
            return cached;

        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var membership =
            await db.TenantMembers
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.TenantId == tenantId && x.Tenant.Active)
                .Select(x => new TenantMembership(x.TenantId, x.Tenant.Name, x.Role))
                .FirstOrDefaultAsync(cancellationToken);

        cache.Set(key, membership, MembershipCache);

        return membership;
    }

    private void Forget(
        string userId,
        string tenantId) =>
        cache.Remove(CacheKey(userId, tenantId));

    private static string CacheKey(
        string userId,
        string tenantId) =>
        $"rs:membership:{userId}:{tenantId.ToLowerInvariant()}";

    // ------------------------------------------------------------ miembros

    public async Task<IReadOnlyList<TenantMemberInfo>> ListMembersAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        return await db.TenantMembers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.User.Email)
            .Select(x => new TenantMemberInfo(
                x.UserId,
                x.User.Email ?? string.Empty,
                x.User.DisplayName,
                x.Role,
                x.CreatedAtUtc,
                x.User.LockoutEnd != null && x.User.LockoutEnd > DateTimeOffset.UtcNow))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TenantInvitationInfo>> ListPendingInvitationsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var now = time.GetUtcNow().UtcDateTime;

        return await db.TenantInvitations
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.AcceptedAtUtc == null &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new TenantInvitationInfo(
                x.InvitationId,
                x.TenantId,
                x.Tenant.Name,
                x.Email,
                x.Role,
                x.CreatedAtUtc,
                x.ExpiresAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Invita por correo. Un Admin puede invitar a Member y Admin; sólo un
    /// Owner puede invitar a otro Owner. Una invitación pendiente al mismo
    /// correo se sustituye.
    /// </summary>
    public async Task<TenantOperationResult> InviteAsync(
        string tenantId,
        TenantActor actor,
        string emailAddress,
        TenantRole role,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var verified = await VerifyActorAsync(tenantId, actor, cancellationToken);

        return verified is null
            ? NoLongerMember()
            : await InviteCoreAsync(tenantId, verified, emailAddress, role, baseUrl, cancellationToken);
    }

    private async Task<TenantOperationResult> InviteCoreAsync(
        string tenantId,
        TenantActor actor,
        string emailAddress,
        TenantRole role,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        if (actor.Role < TenantRole.Admin)
            return TenantOperationResult.Fail("Sólo un administrador puede invitar.");

        if (role > actor.Role)
            return TenantOperationResult.Fail("No puedes invitar con un rol mayor que el tuyo.");

        var address = (emailAddress ?? string.Empty).Trim();

        if (!IsEmail(address))
            return TenantOperationResult.Fail("El correo no es válido.");

        var normalized = users.NormalizeEmail(address) ?? address.ToUpperInvariant();

        var alreadyMember =
            await db.TenantMembers.AnyAsync(
                x => x.TenantId == tenantId && x.User.NormalizedEmail == normalized,
                cancellationToken);

        if (alreadyMember)
            return TenantOperationResult.Fail("Ese correo ya es miembro del tenant.");

        var tenant =
            await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (tenant is null)
            return TenantOperationResult.Fail("No existe el tenant.");

        var now = time.GetUtcNow().UtcDateTime;

        // Una invitación pendiente al mismo correo se sustituye: el enlace
        // anterior deja de valer.
        var pending =
            await db.TenantInvitations
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.NormalizedEmail == normalized &&
                    x.AcceptedAtUtc == null &&
                    x.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

        foreach (var old in pending)
            old.RevokedAtUtc = now;

        var token = NewToken();

        db.TenantInvitations.Add(
            new TenantInvitationRow
            {
                InvitationId = Guid.NewGuid(),
                TenantId = tenantId,
                Email = address,
                NormalizedEmail = normalized,
                Role = role,
                TokenHash = Hash(token),
                CreatedByUserId = actor.UserId,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(InvitationLifetime)
            });

        await db.SaveChangesAsync(cancellationToken);

        var link = $"{baseUrl.TrimEnd('/')}/account/invitation?token={Uri.EscapeDataString(token)}";

        try
        {
            await email.SendAsync(
                address,
                $"Invitación a {tenant.Name} en RenderSet",
                EmailTemplates.Invitation(tenant.Name, actor.DisplayName, role, link, InvitationLifetime),
                cancellationToken);
        }
        catch (Exception exception)
        {
            // La invitación queda creada: se puede reenviar.
            logger.LogError(exception, "No se ha podido enviar la invitación a {Email} ({TenantId}).", address, tenantId);

            return TenantOperationResult.Fail(
                "La invitación se ha creado, pero no se ha podido enviar el correo. Inténtalo de nuevo en unos minutos.");
        }

        return TenantOperationResult.Ok();
    }

    public async Task<TenantOperationResult> RevokeInvitationAsync(
        string tenantId,
        TenantActor actor,
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        if (await VerifyActorAsync(db, tenantId, actor, cancellationToken) is not { } verified)
            return NoLongerMember();

        actor = verified;

        if (actor.Role < TenantRole.Admin)
            return TenantOperationResult.Fail("Sólo un administrador puede anular invitaciones.");

        var invitation =
            await db.TenantInvitations.FirstOrDefaultAsync(
                x => x.InvitationId == invitationId && x.TenantId == tenantId,
                cancellationToken);

        if (invitation is null)
            return TenantOperationResult.Fail("No existe la invitación.");

        invitation.RevokedAtUtc ??= time.GetUtcNow().UtcDateTime;

        await db.SaveChangesAsync(cancellationToken);

        return TenantOperationResult.Ok();
    }

    /// <summary>
    /// Sólo un Owner toca a otros Owner o nombra Owner. Siempre queda al
    /// menos un Owner.
    /// </summary>
    public async Task<TenantOperationResult> ChangeRoleAsync(
        string tenantId,
        TenantActor actor,
        string userId,
        TenantRole role,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        if (await VerifyActorAsync(db, tenantId, actor, cancellationToken) is not { } verified)
            return NoLongerMember();

        actor = verified;

        if (actor.Role < TenantRole.Admin)
            return TenantOperationResult.Fail("Sólo un administrador puede cambiar roles.");

        var member =
            await db.TenantMembers.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.UserId == userId,
                cancellationToken);

        if (member is null)
            return TenantOperationResult.Fail("No es miembro del tenant.");

        if (member.Role == role)
            return TenantOperationResult.Ok();

        if ((member.Role == TenantRole.Owner || role == TenantRole.Owner) && actor.Role != TenantRole.Owner)
            return TenantOperationResult.Fail("Sólo un propietario puede cambiar a otro propietario o nombrarlo.");

        if (member.Role == TenantRole.Owner && await IsLastOwnerAsync(db, tenantId, cancellationToken))
            return TenantOperationResult.Fail("Es el único propietario: nombra antes a otro.");

        member.Role = role;

        await db.SaveChangesAsync(cancellationToken);

        Forget(userId, tenantId);

        return TenantOperationResult.Ok();
    }

    public async Task<TenantOperationResult> RemoveMemberAsync(
        string tenantId,
        TenantActor actor,
        string userId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        if (await VerifyActorAsync(db, tenantId, actor, cancellationToken) is not { } verified)
            return NoLongerMember();

        actor = verified;

        var self = userId == actor.UserId;

        if (!self && actor.Role < TenantRole.Admin)
            return TenantOperationResult.Fail("Sólo un administrador puede quitar miembros.");

        var member =
            await db.TenantMembers.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.UserId == userId,
                cancellationToken);

        if (member is null)
            return TenantOperationResult.Ok();

        if (member.Role == TenantRole.Owner && !self && actor.Role != TenantRole.Owner)
            return TenantOperationResult.Fail("Sólo un propietario puede quitar a otro propietario.");

        if (member.Role == TenantRole.Owner && await IsLastOwnerAsync(db, tenantId, cancellationToken))
            return TenantOperationResult.Fail("Es el único propietario: nombra antes a otro.");

        db.TenantMembers.Remove(member);

        await db.SaveChangesAsync(cancellationToken);

        Forget(userId, tenantId);

        return TenantOperationResult.Ok();
    }

    private static async Task<bool> IsLastOwnerAsync(
        IdentityDb db,
        string tenantId,
        CancellationToken cancellationToken) =>
        await db.TenantMembers.CountAsync(
            x => x.TenantId == tenantId && x.Role == TenantRole.Owner,
            cancellationToken) <= 1;

    // ------------------------------------------------------------ invitaciones

    /// <summary>
    /// Invitación vigente para un token, o nulo (no existe, caducada,
    /// anulada o ya usada).
    /// </summary>
    public async Task<TenantInvitationInfo?> FindInvitationAsync(
        string? token,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var row = await FindInvitationRowAsync(db, token, cancellationToken);

        return row is null
            ? null
            : new TenantInvitationInfo(
                row.InvitationId,
                row.TenantId,
                row.Tenant.Name,
                row.Email,
                row.Role,
                row.CreatedAtUtc,
                row.ExpiresAtUtc);
    }

    /// <summary>
    /// Da de alta la pertenencia de <paramref name="user"/> y marca la
    /// invitación como usada. El correo del usuario tiene que ser el
    /// invitado.
    /// </summary>
    public async Task<TenantOperationResult> AcceptInvitationAsync(
        string token,
        RendersetUser user,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var invitation = await FindInvitationRowAsync(db, token, cancellationToken);

        if (invitation is null)
            return TenantOperationResult.Fail("La invitación no existe, ha caducado o ya se ha usado.");

        if (!string.Equals(invitation.NormalizedEmail, user.NormalizedEmail, StringComparison.Ordinal))
            return TenantOperationResult.Fail("La invitación es para otro correo.");

        var now = time.GetUtcNow().UtcDateTime;

        var member =
            await db.TenantMembers.FirstOrDefaultAsync(
                x => x.TenantId == invitation.TenantId && x.UserId == user.Id,
                cancellationToken);

        if (member is null)
        {
            db.TenantMembers.Add(
                new TenantMemberRow
                {
                    TenantId = invitation.TenantId,
                    UserId = user.Id,
                    Role = invitation.Role,
                    CreatedAtUtc = now
                });
        }
        else if (invitation.Role > member.Role)
        {
            member.Role = invitation.Role;
        }

        invitation.AcceptedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        Forget(user.Id, invitation.TenantId);

        return TenantOperationResult.Ok(invitation.TenantId);
    }

    private async Task<TenantInvitationRow?> FindInvitationRowAsync(
        IdentityDb db,
        string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200)
            return null;

        var hash = Hash(token);
        var now = time.GetUtcNow().UtcDateTime;

        return await db.TenantInvitations
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(
                x => x.TokenHash == hash &&
                     x.AcceptedAtUtc == null &&
                     x.RevokedAtUtc == null &&
                     x.ExpiresAtUtc > now &&
                     x.Tenant.Active,
                cancellationToken);
    }

    /// <summary>
    /// Primer propietario de un tenant (arranque): si nadie es Owner y no
    /// hay una invitación de Owner pendiente para ese correo, la crea y la
    /// envía. Devuelve qué ha pasado, o nulo si no hacía falta.
    /// </summary>
    public async Task<string?> EnsureOwnerInvitationAsync(
        string tenantId,
        string emailAddress,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        var hasOwner =
            await db.TenantMembers.AnyAsync(
                x => x.TenantId == tenantId && x.Role == TenantRole.Owner,
                cancellationToken);

        if (hasOwner)
            return null;

        var normalized = users.NormalizeEmail(emailAddress.Trim()) ?? emailAddress.Trim().ToUpperInvariant();
        var now = time.GetUtcNow().UtcDateTime;

        // Si ya hay una invitación de propietario vigente para ese correo, no
        // se manda otra: un reinicio del App Service no puede llenar el buzón.
        var pending =
            await db.TenantInvitations.AnyAsync(
                x => x.TenantId == tenantId &&
                     x.NormalizedEmail == normalized &&
                     x.Role == TenantRole.Owner &&
                     x.AcceptedAtUtc == null &&
                     x.RevokedAtUtc == null &&
                     x.ExpiresAtUtc > now,
                cancellationToken);

        if (pending)
            return null;

        var actor = new TenantActor("bootstrap", "RenderSet", TenantRole.Owner);

        // El arranque no es un miembro: se invita sin comprobar su rol.
        var result = await InviteCoreAsync(tenantId, actor, emailAddress, TenantRole.Owner, baseUrl, cancellationToken);

        return result.Succeeded
            ? $"Invitación de propietario enviada a {emailAddress}."
            : result.Error;
    }

    // ------------------------------------------------------------ quién lo pide

    /// <summary>
    /// El rol de quien hace la operación se vuelve a leer de la base de
    /// datos, sin caché: una página de miembros abierta guarda el rol de
    /// cuando se cargó, y a esa persona la pueden haber bajado de rol o
    /// quitado del tenant entretanto.
    /// </summary>
    private async Task<TenantActor?> VerifyActorAsync(
        string tenantId,
        TenantActor actor,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);

        return await VerifyActorAsync(db, tenantId, actor, cancellationToken);
    }

    private static async Task<TenantActor?> VerifyActorAsync(
        IdentityDb db,
        string tenantId,
        TenantActor actor,
        CancellationToken cancellationToken)
    {
        var role =
            await db.TenantMembers
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.UserId == actor.UserId && x.Tenant.Active)
                .Select(x => (TenantRole?)x.Role)
                .FirstOrDefaultAsync(cancellationToken);

        return role is null
            ? null
            : actor with { Role = role.Value };
    }

    private static TenantOperationResult NoLongerMember() =>
        TenantOperationResult.Fail("Ya no tienes acceso a este tenant. Recarga la página.");

    // ------------------------------------------------------------ utilidades

    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Hash(
        string token) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static bool IsEmail(
        string value) =>
        value.Length is > 3 and <= 256 &&
        new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value);
}


public sealed record TenantMembership(
    string TenantId,
    string TenantName,
    TenantRole Role);


public sealed record TenantMemberInfo(
    string UserId,
    string Email,
    string? DisplayName,
    TenantRole Role,
    DateTime SinceUtc,
    bool LockedOut);


public sealed record TenantInvitationInfo(
    Guid InvitationId,
    string TenantId,
    string TenantName,
    string Email,
    TenantRole Role,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);


/// <summary>Quién hace la operación, con su rol en el tenant.</summary>
public sealed record TenantActor(
    string UserId,
    string DisplayName,
    TenantRole Role);


public sealed record TenantOperationResult(
    bool Succeeded,
    string? Error,
    string? TenantId = null)
{
    public static TenantOperationResult Ok(string? tenantId = null) => new(true, null, tenantId);

    public static TenantOperationResult Fail(string error) => new(false, error);
}
