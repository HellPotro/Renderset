using Microsoft.EntityFrameworkCore;
using Renderset.Core.Tenancy;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;
using Renderset.Infrastructure.Storage;

namespace Renderset.Infrastructure.Repositories;

/// <summary>
/// Imágenes de los tenants en la base de datos de RenderSet, con el
/// contenido en la fila o, con Assets:Storage, en su almacén.
///
/// Sólo se añade: la misma imagen tiene siempre el mismo id, y un logo
/// que ya no se usa no estorba (los PDF ya emitidos lo llevan dentro).
/// </summary>
public sealed class EfTenantAssetRepository
    : ITenantAssetRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;
    private readonly TenantAssetContentStore? _store;

    public EfTenantAssetRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory,
        TenantAssetContentStore? store = null)
    {
        _contextFactory = contextFactory;
        _store = store;
    }

    public async Task<TenantAsset> SaveAsync(
        TenantAsset asset,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var existing =
            await context.Set<TenantAssetEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == asset.TenantId && x.AssetId == asset.AssetId,
                    cancellationToken);

        if (existing is not null)
            return ToModel(existing);

        var entity =
            new TenantAssetEntity
            {
                TenantId = asset.TenantId,
                AssetId = asset.AssetId,
                ContentType = asset.ContentType,
                FileName = asset.FileName,
                SizeBytes = content.LongLength,
                CreatedAtUtc = asset.CreatedAtUtc,
                CreatedBy = asset.CreatedBy
            };

        if (_store is null)
        {
            entity.Content = content;
        }
        else
        {
            entity.Path =
                await _store.SaveAsync(
                    asset.TenantId,
                    asset.AssetId,
                    asset.PublicFileName,
                    asset.ContentType,
                    content,
                    cancellationToken);
        }

        context.Add(entity);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dos subidas de la misma imagen a la vez: la otra ya la ha
            // guardado, y es idéntica.
            var raced =
                await context.Set<TenantAssetEntity>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.TenantId == asset.TenantId && x.AssetId == asset.AssetId,
                        cancellationToken);

            if (raced is null)
                throw;

            return ToModel(raced);
        }

        return ToModel(entity);
    }

    public async Task<(TenantAsset Asset, byte[] Content)?> GetAsync(
        string tenantId,
        string assetId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity =
            await context.Set<TenantAssetEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.AssetId == assetId,
                    cancellationToken);

        if (entity is null)
            return null;

        var content = entity.Content;

        if (content is null && !string.IsNullOrWhiteSpace(entity.Path) && _store is not null)
            content = await _store.ReadAsync(entity.Path, cancellationToken);

        return content is null
            ? null
            : (ToModel(entity), content);
    }

    private static TenantAsset ToModel(
        TenantAssetEntity entity) =>
        new()
        {
            TenantId = entity.TenantId,
            AssetId = entity.AssetId,
            ContentType = entity.ContentType,
            FileName = entity.FileName,
            SizeBytes = entity.SizeBytes,
            CreatedAtUtc = DateTime.SpecifyKind(entity.CreatedAtUtc, DateTimeKind.Utc),
            CreatedBy = entity.CreatedBy
        };
}
