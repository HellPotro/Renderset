using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Renderset.Core.Mapping;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfDataMappingRepository
    : IDataMappingRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public EfDataMappingRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyCollection<DataMapping>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.DataMappings
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);

        return entities
            .Select(ToModel)
            .ToList();
    }

    public async Task<DataMapping?> GetByIdAsync(
        string tenantId,
        string mappingId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.DataMappings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.MappingId == mappingId &&
                        x.Active,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task<DataMapping?> GetForReportAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.DataMappings
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ReportId == reportId &&
                    x.Active)
                .OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task SaveAsync(
        string tenantId,
        DataMapping mapping,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                cancellationToken);

        var now = DateTime.UtcNow;

        var reportId =
            string.IsNullOrWhiteSpace(mapping.ReportId)
                ? null
                : mapping.ReportId.Trim();

        // Un report usa un solo mapping: el que se asigna ahora gana.
        if (reportId is not null)
        {
            await context.DataMappings
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.MappingId != mapping.Id &&
                    x.ReportId == reportId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.ReportId, (string?)null)
                        .SetProperty(x => x.UpdatedAtUtc, now),
                    cancellationToken);
        }

        var json =
            JsonSerializer.Serialize(
                mapping.Root,
                JsonOptions);

        var entity =
            await context.DataMappings
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.MappingId == mapping.Id,
                    cancellationToken);

        if (entity is null)
        {
            context.DataMappings.Add(
                new DataMappingEntity
                {
                    TenantId = tenantId,
                    MappingId = mapping.Id,
                    Name = mapping.Name.Trim(),
                    ReportId = reportId,
                    MappingJson = json,
                    Version = 1,
                    Active = true,
                    CreatedAtUtc = now
                });
        }
        else
        {
            // Cada cambio sube la versión: deja rastro de que el mapping con
            // el que se emitió un documento antiguo ya no es el mismo.
            entity.Name = mapping.Name.Trim();
            entity.ReportId = reportId;
            entity.MappingJson = json;
            entity.Version = entity.Active ? entity.Version + 1 : 1;
            entity.Active = true;
            entity.UpdatedAtUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string tenantId,
        string mappingId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var now = DateTime.UtcNow;

        var affected =
            await context.DataMappings
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.MappingId == mappingId &&
                    x.Active)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Active, false)
                        .SetProperty(x => x.ReportId, (string?)null)
                        .SetProperty(x => x.UpdatedAtUtc, now),
                    cancellationToken);

        return affected > 0;
    }

    private static DataMapping ToModel(
        DataMappingEntity entity)
    {
        var root =
            JsonSerializer.Deserialize<DataMappingNode>(
                entity.MappingJson,
                JsonOptions)
            ?? new DataMappingNode();

        return new DataMapping
        {
            Id = entity.MappingId,
            Name = entity.Name,
            ReportId = entity.ReportId,
            Root = root,
            Version = entity.Version,
            UpdatedAtUtc = entity.UpdatedAtUtc ?? entity.CreatedAtUtc
        };
    }
}
