using Microsoft.EntityFrameworkCore;
using Renderset.Core.Security;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfApiKeyRepository
    : IApiKeyRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfApiKeyRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ApiKey?> FindByHashAsync(
        byte[] hash,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.KeyHash == hash,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task<IReadOnlyList<ApiKey>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.ApiKeys
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

        return entities
            .Select(ToModel)
            .ToList();
    }

    public async Task CreateAsync(
        ApiKey key,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        context.ApiKeys.Add(
            new ApiKeyEntity
            {
                ApiKeyId = key.Id,
                TenantId = key.TenantId,
                Name = key.Name,
                Prefix = key.Prefix,
                KeyHash = key.Hash,
                CreatedAtUtc = key.CreatedAtUtc,
                ExpiresAtUtc = key.ExpiresAtUtc
            });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(
        string tenantId,
        Guid keyId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var updated =
            await context.ApiKeys
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ApiKeyId == keyId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.RevokedAtUtc,
                            x => x.RevokedAtUtc ?? (DateTime?)revokedAtUtc),
                    cancellationToken);

        return updated > 0;
    }

    public async Task TouchAsync(
        Guid keyId,
        DateTime usedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        await context.ApiKeys
            .Where(x => x.ApiKeyId == keyId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.LastUsedAtUtc, (DateTime?)usedAtUtc),
                cancellationToken);
    }

    private static ApiKey ToModel(
        ApiKeyEntity entity) =>
        new()
        {
            Id = entity.ApiKeyId,
            TenantId = entity.TenantId,
            Name = entity.Name,
            Prefix = entity.Prefix,
            Hash = entity.KeyHash,
            CreatedAtUtc = AsUtc(entity.CreatedAtUtc),
            LastUsedAtUtc = entity.LastUsedAtUtc is { } used ? AsUtc(used) : null,
            ExpiresAtUtc = entity.ExpiresAtUtc is { } expires ? AsUtc(expires) : null,
            RevokedAtUtc = entity.RevokedAtUtc is { } revoked ? AsUtc(revoked) : null
        };

    private static DateTime AsUtc(
        DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
