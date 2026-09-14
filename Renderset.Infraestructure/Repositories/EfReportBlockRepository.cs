using Microsoft.EntityFrameworkCore;
using Renderset.Core.Blocks;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfReportBlockRepository
    : IReportBlockRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfReportBlockRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ReportBlock?> GetByIdAsync(
        string tenantId,
        string blockId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportBlocks
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.BlockId == blockId &&
                        x.Active,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task<IReadOnlyCollection<ReportBlock>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.ReportBlocks
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Active)
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

        return entities
            .Select(ToModel)
            .ToList();
    }

    public async Task<IReadOnlyCollection<ReportBlock>> GetByTypeAsync(
        string tenantId,
        ReportBlockType type,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.ReportBlocks
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Type == type &&
                    x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

        return entities
            .Select(ToModel)
            .ToList();
    }

    public async Task SaveAsync(
        string tenantId,
        ReportBlock block,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(block);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportBlocks
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.BlockId == block.Id,
                    cancellationToken);

        if (entity is null)
        {
            entity = new ReportBlockEntity
            {
                TenantId = tenantId,
                BlockId = block.Id,
                Name = block.Name,
                Type = block.Type,
                ConfigurationJson = block.ConfigurationJson,
                Active = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            context.ReportBlocks.Add(entity);
        }
        else
        {
            entity.Name = block.Name;
            entity.Type = block.Type;
            entity.ConfigurationJson = block.ConfigurationJson;
            entity.Active = true;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(
            cancellationToken);
    }

    private static ReportBlock ToModel(
        ReportBlockEntity entity)
    {
        return new ReportBlock
        {
            Id = entity.BlockId,
            Name = entity.Name,
            Type = entity.Type,
            ConfigurationJson = entity.ConfigurationJson
        };
    }
}