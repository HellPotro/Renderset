using Microsoft.EntityFrameworkCore;
using Renderset.Core.Paging;
using Renderset.Core.Sharing;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfDocumentBundleRepository
    : IDocumentBundleRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfDocumentBundleRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task CreateAsync(
        DocumentBundle bundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            new DocumentBundleEntity
            {
                BundleId = bundle.Id,
                TenantId = bundle.TenantId,
                Title = bundle.Title,
                Message = bundle.Message,
                Culture = bundle.Culture,
                TokenHash = bundle.TokenHash,
                ProtectedToken = bundle.ProtectedToken,
                TokenIssuedAtUtc = bundle.TokenIssuedAtUtc,
                AllowDataDownload = bundle.AllowDataDownload,
                ExpiresAtUtc = bundle.ExpiresAtUtc,
                RevokedAtUtc = bundle.RevokedAtUtc,
                CreatedAtUtc = bundle.CreatedAtUtc,
                Items = bundle.Items
                    .Select(item => new DocumentBundleItemEntity
                    {
                        BundleId = bundle.Id,
                        Position = item.Position,
                        TenantId = bundle.TenantId,
                        DocumentId = item.DocumentId,
                        DisplayName = item.DisplayName
                    })
                    .ToList()
            };

        context.DocumentBundles.Add(entity);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DocumentBundle?> GetAsync(
        string tenantId,
        Guid bundleId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.DocumentBundles
                .AsNoTracking()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.BundleId == bundleId,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task<DocumentBundle?> FindAsync(
        Guid bundleId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.DocumentBundles
                .AsNoTracking()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(
                    x => x.BundleId == bundleId,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task<PagedResult<DocumentBundle>> SearchAsync(
        string tenantId,
        DocumentBundleQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var bundles =
            context.DocumentBundles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            bundles = bundles.Where(x =>
                x.Title.Contains(search) ||
                x.Items.Any(i =>
                    i.DisplayName.Contains(search) ||
                    i.DocumentId.Contains(search)));
        }

        // Mismas reglas que DocumentBundle.GetStatus, pero en SQL.
        var now = query.NowUtc;

        bundles = query.Status switch
        {
            DocumentBundleStatus.Revoked =>
                bundles.Where(x => x.RevokedAtUtc != null),

            DocumentBundleStatus.Expired =>
                bundles.Where(x => x.RevokedAtUtc == null && x.ExpiresAtUtc <= now),

            DocumentBundleStatus.Active =>
                bundles.Where(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now),

            _ => bundles
        };

        if (query.FromUtc is { } fromUtc)
            bundles = bundles.Where(x => x.CreatedAtUtc >= fromUtc);

        if (query.ToUtc is { } toUtc)
            bundles = bundles.Where(x => x.CreatedAtUtc < toUtc);

        if (query.Cursor is { } cursor)
        {
            var at = cursor.CreatedAtUtc;

            var seen =
                cursor.SeenIds
                    .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                    .Where(x => x != Guid.Empty)
                    .ToList();

            bundles = bundles.Where(x =>
                x.CreatedAtUtc < at ||
                (x.CreatedAtUtc == at && !seen.Contains(x.BundleId)));
        }

        var take = Math.Clamp(query.Take, 1, DocumentBundleQuery.MaxTake);

        var entities =
            await bundles
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.BundleId)
                .Take(take + 1)
                .Include(x => x.Items)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

        return KeysetCursor.Page(
            entities.Select(ToModel).ToList(),
            take,
            x => x.CreatedAtUtc,
            x => x.Id.ToString("D"));
    }

    public async Task<bool> RevokeAsync(
        string tenantId,
        Guid bundleId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        // Revocar dos veces deja la primera fecha: es la que importa para
        // saber desde cuándo el enlace no vale.
        var updated =
            await context.DocumentBundles
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.BundleId == bundleId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.RevokedAtUtc,
                            x => x.RevokedAtUtc ?? (DateTime?)revokedAtUtc),
                    cancellationToken);

        return updated > 0;
    }

    public async Task<bool> ReplaceLinkAsync(
        string tenantId,
        Guid bundleId,
        byte[] tokenHash,
        string? protectedToken,
        DateTime issuedAtUtc,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var updated =
            await context.DocumentBundles
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.BundleId == bundleId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.TokenHash, tokenHash)
                        .SetProperty(x => x.ProtectedToken, protectedToken)
                        .SetProperty(x => x.TokenIssuedAtUtc, issuedAtUtc)
                        .SetProperty(x => x.ExpiresAtUtc, expiresAtUtc)
                        .SetProperty(x => x.RevokedAtUtc, (DateTime?)null),
                    cancellationToken);

        return updated > 0;
    }

    public async Task RecordAccessAsync(
        DocumentBundleAccess access,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(access);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        context.DocumentBundleAccesses.Add(
            new DocumentBundleAccessEntity
            {
                BundleId = access.BundleId,
                TenantId = access.TenantId,
                Kind = access.Kind.ToString(),
                Position = access.Position,
                AccessedAtUtc = access.AccessedAtUtc,
                IpAddress = access.IpAddress,
                UserAgent = access.UserAgent
            });

        await context.SaveChangesAsync(cancellationToken);

        // El contador se actualiza en la propia sentencia y no leyendo y
        // escribiendo: dos aperturas a la vez no pueden pisarse la cuenta.
        var accessedAtUtc = (DateTime?)access.AccessedAtUtc;

        await context.DocumentBundles
            .Where(x => x.BundleId == access.BundleId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.AccessCount, x => x.AccessCount + 1)
                    .SetProperty(x => x.LastAccessedAtUtc, accessedAtUtc)
                    .SetProperty(
                        x => x.FirstAccessedAtUtc,
                        x => x.FirstAccessedAtUtc ?? accessedAtUtc),
                cancellationToken);
    }

    private static DocumentBundle ToModel(
        DocumentBundleEntity entity)
    {
        return new DocumentBundle
        {
            Id = entity.BundleId,
            TenantId = entity.TenantId,
            Title = entity.Title,
            Message = entity.Message,
            Culture = entity.Culture,
            TokenHash = entity.TokenHash,
            ProtectedToken = entity.ProtectedToken,
            TokenIssuedAtUtc = AsUtc(entity.TokenIssuedAtUtc),
            AllowDataDownload = entity.AllowDataDownload,
            ExpiresAtUtc = AsUtc(entity.ExpiresAtUtc),
            RevokedAtUtc = AsUtc(entity.RevokedAtUtc),
            CreatedAtUtc = AsUtc(entity.CreatedAtUtc),
            AccessCount = entity.AccessCount,
            FirstAccessedAtUtc = AsUtc(entity.FirstAccessedAtUtc),
            LastAccessedAtUtc = AsUtc(entity.LastAccessedAtUtc),
            Items = entity.Items
                .OrderBy(x => x.Position)
                .Select(x => new DocumentBundleItem
                {
                    Position = x.Position,
                    DocumentId = x.DocumentId,
                    DisplayName = x.DisplayName
                })
                .ToList()
        };
    }

    /// <summary>
    /// SQL Server devuelve datetime2 sin zona (Kind = Unspecified). Se marca
    /// como UTC para que la comparación con la hora actual y la
    /// serialización ("Z" al final) sean correctas.
    /// </summary>
    private static DateTime AsUtc(
        DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(
        DateTime? value) =>
        value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;
}
