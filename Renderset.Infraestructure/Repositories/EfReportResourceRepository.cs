using Microsoft.EntityFrameworkCore;
using Renderset.Core.Localization;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfReportResourceRepository
    : IReportResourceRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    public EfReportResourceRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyCollection<ReportResource>> GetByScopeAsync(
        string tenantId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        return await GetByScopesAsync(
            tenantId,
            [scope],
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReportResource>> GetByScopesAsync(
        string tenantId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default)
    {
        if (scopes.Count == 0)
            return [];

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var scopeList = scopes.ToList();

        return await context.ReportResources
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Active &&
                scopeList.Contains(x.Scope))
            .OrderBy(x => x.Scope)
            .ThenBy(x => x.ResourceKey)
            .Select(x => new ReportResource
            {
                Scope = x.Scope,
                Key = x.ResourceKey,
                Culture = x.Culture,
                Value = x.Value,
                Source = x.Source,
                Description = x.Description
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReportResourceCoverage>> GetCoverageAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        return await context.ReportResources
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Active)
            .GroupBy(x => new
            {
                x.Scope,
                x.Culture
            })
            .Select(g => new ReportResourceCoverage
            {
                Scope = g.Key.Scope,
                Culture = g.Key.Culture,
                TotalKeys = g.Count(),
                TranslatedKeys = g.Count(x => x.Value != null && x.Value != ""),
                MachineKeys = g.Count(x => x.Source == ReportResourceSource.Machine)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(
        string tenantId,
        ReportResource resource,
        CancellationToken cancellationToken = default)
    {
        await SaveManyAsync(
            tenantId,
            [resource],
            cancellationToken);
    }

    public async Task SaveManyAsync(
        string tenantId,
        IReadOnlyCollection<ReportResource> resources,
        CancellationToken cancellationToken = default)
    {
        if (resources.Count == 0)
            return;

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var existing =
            await LoadExistingAsync(
                context,
                tenantId,
                resources,
                cancellationToken);

        foreach (var resource in resources)
        {
            var key = BuildKey(
                resource.Scope,
                resource.Key,
                resource.Culture);

            if (existing.TryGetValue(key, out var entity))
            {
                entity.Value = resource.Value;
                entity.Source = resource.Source;
                entity.Description = resource.Description;
                entity.Active = true;
                entity.UpdatedAtUtc = DateTime.UtcNow;

                continue;
            }

            context.ReportResources.Add(
                new ReportResourceEntity
                {
                    TenantId = tenantId,
                    Scope = resource.Scope,
                    ResourceKey = resource.Key,
                    Culture = resource.Culture,
                    Value = resource.Value,
                    Source = resource.Source,
                    Description = resource.Description,
                    Active = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SeedMissingAsync(
        string tenantId,
        IReadOnlyCollection<ReportResource> resources,
        CancellationToken cancellationToken = default)
    {
        if (resources.Count == 0)
            return;

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var existing =
            await LoadExistingAsync(
                context,
                tenantId,
                resources,
                cancellationToken);

        foreach (var resource in resources)
        {
            var key = BuildKey(
                resource.Scope,
                resource.Key,
                resource.Culture);

            if (existing.TryGetValue(key, out var entity))
            {
                // La clave ya existe: sólo se reactiva y se rellena si estaba
                // vacía. Nunca se pisa una traducción existente al resembrar.
                if (!entity.Active)
                {
                    entity.Active = true;
                    entity.UpdatedAtUtc = DateTime.UtcNow;
                }

                if (string.IsNullOrWhiteSpace(entity.Value) &&
                    !string.IsNullOrWhiteSpace(resource.Value))
                {
                    entity.Value = resource.Value;
                    entity.Source = resource.Source;
                    entity.UpdatedAtUtc = DateTime.UtcNow;
                }

                continue;
            }

            context.ReportResources.Add(
                new ReportResourceEntity
                {
                    TenantId = tenantId,
                    Scope = resource.Scope,
                    ResourceKey = resource.Key,
                    Culture = resource.Culture,
                    Value = resource.Value,
                    Source = resource.Source,
                    Description = resource.Description,
                    Active = true,
                    CreatedAtUtc = DateTime.UtcNow
                });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string tenantId,
        string scope,
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        // Se borra la clave en todas las culturas: una clave a medias no
        // tiene sentido.
        var entities =
            await context.ReportResources
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Scope == scope &&
                    x.ResourceKey == key)
                .ToListAsync(cancellationToken);

        if (entities.Count == 0)
            return;

        foreach (var entity in entities)
        {
            entity.Active = false;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, ReportResourceEntity>> LoadExistingAsync(
        RenderSetDbContext context,
        string tenantId,
        IReadOnlyCollection<ReportResource> resources,
        CancellationToken cancellationToken)
    {
        var scopes = resources
            .Select(x => x.Scope)
            .Distinct()
            .ToList();

        var keys = resources
            .Select(x => x.Key)
            .Distinct()
            .ToList();

        var entities =
            await context.ReportResources
                .Where(x =>
                    x.TenantId == tenantId &&
                    scopes.Contains(x.Scope) &&
                    keys.Contains(x.ResourceKey))
                .ToListAsync(cancellationToken);

        return entities.ToDictionary(
            x => BuildKey(
                x.Scope,
                x.ResourceKey,
                x.Culture),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildKey(
        string scope,
        string key,
        string culture)
    {
        return $"{scope}|{key}|{culture}";
    }
}
