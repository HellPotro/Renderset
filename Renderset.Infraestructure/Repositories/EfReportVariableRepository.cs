using Microsoft.EntityFrameworkCore;
using Renderset.Core.Variables;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

public sealed class EfReportVariableRepository
    : IReportVariableRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfReportVariableRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyCollection<ReportVariable>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        return await context.ReportVariables
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Active)
            .OrderBy(x => x.VariableKey)
            .Select(x => new ReportVariable
            {
                Key = x.VariableKey,
                Value = x.Value,
                Description = x.Description,
                Type = x.Type,
                Translatable = x.Translatable
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ReportVariable?> GetAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportVariables
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.VariableKey == key &&
                        x.Active,
                    cancellationToken);

        return entity is null
            ? null
            : new ReportVariable
            {
                Key = entity.VariableKey,
                Value = entity.Value,
                Description = entity.Description,
                Type = entity.Type,
                Translatable = entity.Translatable
            };
    }

    public async Task SaveAsync(
        string tenantId,
        ReportVariable variable,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportVariables
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.VariableKey == variable.Key,
                    cancellationToken);

        if (entity is null)
        {
            entity = new ReportVariableEntity
            {
                TenantId = tenantId,
                VariableKey = variable.Key,
                Value = variable.Value,
                Description = variable.Description,
                Type = variable.Type,
                Translatable = variable.Translatable,
                Active = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            context.ReportVariables.Add(entity);
        }
        else
        {
            entity.Value = variable.Value;
            entity.Description = variable.Description;
            entity.Type = variable.Type;
            entity.Translatable = variable.Translatable;
            entity.Active = true;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
        string tenantId,
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportVariables
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.VariableKey == key,
                    cancellationToken);

        if (entity is null)
            return;

        entity.Active = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(
            cancellationToken);
    }
}