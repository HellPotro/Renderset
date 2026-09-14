using Microsoft.EntityFrameworkCore;
using Renderset.Core.Localization;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfTenantCultureRepository
    : ITenantCultureRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfTenantCultureRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyCollection<TenantCulture>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        return await context.TenantCultures
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Active)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Culture)
            .Select(x => new TenantCulture
            {
                Culture = x.Culture,
                DisplayName = x.DisplayName,
                IsDefault = x.IsDefault
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantCulture?> GetDefaultAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        return await context.TenantCultures
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Active &&
                x.IsDefault)
            .Select(x => new TenantCulture
            {
                Culture = x.Culture,
                DisplayName = x.DisplayName,
                IsDefault = x.IsDefault
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(
        string tenantId,
        TenantCulture culture,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        if (culture.IsDefault)
        {
            // El índice único filtrado rechazaría dos por defecto, así que se
            // baja el anterior antes de subir el nuevo.
            var currentDefaults =
                await context.TenantCultures
                    .Where(x =>
                        x.TenantId == tenantId &&
                        x.IsDefault &&
                        x.Culture != culture.Culture)
                    .ToListAsync(cancellationToken);

            foreach (var entity in currentDefaults)
            {
                entity.IsDefault = false;
                entity.UpdatedAtUtc = DateTime.UtcNow;
            }

            if (currentDefaults.Count > 0)
                await context.SaveChangesAsync(cancellationToken);
        }

        var existing =
            await context.TenantCultures
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.Culture == culture.Culture,
                    cancellationToken);

        if (existing is null)
        {
            context.TenantCultures.Add(
                new TenantCultureEntity
                {
                    TenantId = tenantId,
                    Culture = culture.Culture,
                    DisplayName = culture.DisplayName,
                    IsDefault = culture.IsDefault,
                    Active = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }
        else
        {
            existing.DisplayName = culture.DisplayName;
            existing.IsDefault = culture.IsDefault;
            existing.Active = true;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string tenantId,
        string culture,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.TenantCultures
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.Culture == culture,
                    cancellationToken);

        if (entity is null)
            return;

        if (entity.IsDefault)
        {
            throw new InvalidOperationException(
                "No se puede desactivar el idioma por defecto del tenant.");
        }

        entity.Active = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }
}
