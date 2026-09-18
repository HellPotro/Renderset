using Microsoft.EntityFrameworkCore;
using Renderset.Core.Localization;
using Renderset.Core.Resources;
using Renderset.Core.Variables;
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

        // El agrupado se hace en memoria y no en SQL porque hay que descartar
        // las claves cuyo texto es sólo un marcador de variable, y eso no se
        // puede expresar en una consulta. Se traen únicamente las cuatro
        // columnas que intervienen: son los recursos de un tenant, no una
        // tabla de movimientos.
        var rows =
            await context.ReportResources
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Active)
                .Select(x => new
                {
                    x.Scope,
                    x.ResourceKey,
                    x.Culture,
                    x.Value,
                    x.Source
                })
                .ToListAsync(cancellationToken);

        // Una clave que en algún idioma es sólo un marcador no tiene nada que
        // traducir en ninguno: el texto viene de la variable. Contarla como
        // pendiente dejaría una cobertura que nunca puede llegar al 100% por
        // mucho que se traduzca.
        var excluded =
            rows
                .Where(x => ReportVariableTemplate.IsOnlyTokens(x.Value))
                .Select(x => (x.Scope, x.ResourceKey))
                .ToHashSet();

        return rows
            .Where(x => !excluded.Contains((x.Scope, x.ResourceKey)))
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
                TranslatedKeys = g.Count(x => !string.IsNullOrEmpty(x.Value)),
                MachineKeys = g.Count(x => x.Source == ReportResourceSource.Machine)
            })
            .ToList();
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

    public async Task<CopyMissingReportResourcesResult> CopyMissingAsync(
    string tenantId,
    CopyMissingReportResourcesRequest request,
    CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var sourceCulture =
            request.SourceCulture.Trim();

        var targetCulture =
            request.TargetCulture.Trim();

        var scope =
            request.Scope.Trim();

        if (string.Equals(sourceCulture, targetCulture, StringComparison.OrdinalIgnoreCase))
        {
            return new CopyMissingReportResourcesResult
            {
                Scope = scope,
                SourceCulture = sourceCulture,
                TargetCulture = targetCulture,
                Created = 0,
                Skipped = 0
            };
        }

        var sourceResources =
            await context.ReportResources
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Scope == scope &&
                    x.Culture == sourceCulture &&
                    x.Active)
                .ToListAsync(cancellationToken);

        var targetKeys =
            await context.ReportResources
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Scope == scope &&
                    x.Culture == targetCulture &&
                    x.Active)
                .Select(x => x.ResourceKey)
                .ToListAsync(cancellationToken);

        var existingTargetKeys =
            targetKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now =
            DateTime.UtcNow;

        var created =
            0;

        var skipped =
            0;

        foreach (var source in sourceResources)
        {
            if (existingTargetKeys.Contains(source.ResourceKey))
            {
                skipped++;
                continue;
            }

            context.ReportResources.Add(
                new ReportResourceEntity
                {
                    TenantId = tenantId,
                    Scope = scope,
                    Culture = targetCulture,
                    ResourceKey = source.ResourceKey,
                    Value = request.CopyValue
                        ? source.Value
                        : "",
                    Description = source.Description,
                    Source = ReportResourceSource.Manual,
                    Active = true,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

            created++;
        }

        await context.SaveChangesAsync(cancellationToken);

        return new CopyMissingReportResourcesResult
        {
            Scope = scope,
            SourceCulture = sourceCulture,
            TargetCulture = targetCulture,
            Created = created,
            Skipped = skipped
        };
    }

    private static string BuildKey(
        string scope,
        string key,
        string culture)
    {
        return $"{scope}|{key}|{culture}";
    }
}
