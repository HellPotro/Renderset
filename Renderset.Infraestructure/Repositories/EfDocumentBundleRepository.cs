using Microsoft.EntityFrameworkCore;
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
                TokenIssuedAtUtc = bundle.TokenIssuedAtUtc,
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

    public async Task<IReadOnlyList<DocumentBundle>> ListAsync(
        string tenantId,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.DocumentBundles
                .AsNoTracking()
                .Include(x => x.Items)
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(Math.Clamp(take, 1, 500))
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

        return entities
            .Select(ToModel)
            .ToList();
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
            TokenIssuedAtUtc = AsUtc(entity.TokenIssuedAtUtc),
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
