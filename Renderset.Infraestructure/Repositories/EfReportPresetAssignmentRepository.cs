using Microsoft.EntityFrameworkCore;
using Renderset.Core.Presets;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfReportPresetAssignmentRepository
    : IReportPresetAssignmentRepository
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

    public EfReportPresetAssignmentRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ReportPresetAssignment?> GetAsync(
        string tenantId,
        string reportId,
        string contextType,
        string contextKey,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportPresetAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.ReportId == reportId &&
                        x.ContextType == contextType &&
                        x.ContextKey == contextKey &&
                        x.Active,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task<IReadOnlyCollection<ReportPresetAssignment>> GetAllAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.ReportPresetAssignments
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ReportId == reportId &&
                    x.Active)
                .OrderBy(x => x.ContextType == null ? 0 : 1)
                .ThenBy(x => x.ContextType)
                .ThenBy(x => x.ContextKey)
                .ToListAsync(cancellationToken);

        return entities
            .Select(x => new ReportPresetAssignment
            {
                Id = x.AssignmentId,
                ReportId = x.ReportId,
                ContextType = x.ContextType,
                ContextKey = x.ContextKey,
                PresetId = x.PresetId
            })
            .ToList();
    }

    public async Task<ReportPresetAssignment?> GetDefaultAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportPresetAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.ReportId == reportId &&
                        x.ContextType == null &&
                        x.ContextKey == null &&
                        x.Active,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task SaveAsync(
        string tenantId,
        ReportPresetAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportPresetAssignments
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.ReportId == assignment.ReportId &&
                        x.ContextType == assignment.ContextType &&
                        x.ContextKey == assignment.ContextKey,
                    cancellationToken);

        if (entity is null)
        {
            entity =
                new ReportPresetAssignmentEntity
                {
                    TenantId = tenantId,
                    ReportId = assignment.ReportId,

                    ContextType =
                        assignment.ContextType,

                    ContextKey =
                        assignment.ContextKey,

                    PresetId =
                        assignment.PresetId,

                    Active = true,

                    CreatedAtUtc =
                        DateTime.UtcNow
                };

            context.ReportPresetAssignments.Add(
                entity);
        }
        else
        {
            entity.PresetId =
                assignment.PresetId;

            entity.Active = true;

            entity.UpdatedAtUtc =
                DateTime.UtcNow;
        }

        await context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<int> SaveManyAsync(
        string tenantId,
        IReadOnlyCollection<ReportPresetAssignment> assignments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignments);

        if (assignments.Count == 0)
            return 0;

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        // Una consulta por report, no una por fila: un CSV de quinientos
        // clientes son quinientas filas del mismo report.
        var reportIds =
            assignments
                .Select(x => x.ReportId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        var existing =
            await context.ReportPresetAssignments
                .Where(x =>
                    x.TenantId == tenantId &&
                    reportIds.Contains(x.ReportId))
                .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var saved = 0;

        foreach (var assignment in assignments)
        {
            var entity =
                existing.FirstOrDefault(x =>
                    string.Equals(x.ReportId, assignment.ReportId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.ContextType, assignment.ContextType, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.ContextKey, assignment.ContextKey, StringComparison.OrdinalIgnoreCase));

            if (entity is null)
            {
                entity =
                    new ReportPresetAssignmentEntity
                    {
                        TenantId = tenantId,
                        ReportId = assignment.ReportId,
                        ContextType = assignment.ContextType,
                        ContextKey = assignment.ContextKey,
                        PresetId = assignment.PresetId,
                        Active = true,
                        CreatedAtUtc = now
                    };

                context.ReportPresetAssignments.Add(entity);
                existing.Add(entity);
            }
            else
            {
                entity.PresetId = assignment.PresetId;
                entity.Active = true;
                entity.UpdatedAtUtc = now;
            }

            saved++;
        }

        await context.SaveChangesAsync(
            cancellationToken);

        return saved;
    }

    public async Task<bool> DeleteAsync(
        string tenantId,
        long assignmentId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportPresetAssignments
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.AssignmentId == assignmentId &&
                        x.Active,
                    cancellationToken);

        if (entity is null)
            return false;

        // Se borra de verdad: el índice único por contexto no deja crear
        // otra asignación igual mientras la fila exista, y SaveAsync ya
        // reactiva las inactivas, así que una baja lógica no aporta nada.
        context.ReportPresetAssignments.Remove(entity);

        await context.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    public async Task<int> DeleteByPresetAsync(
        string tenantId,
        string presetId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.ReportPresetAssignments
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.PresetId == presetId)
                .ToListAsync(cancellationToken);

        if (entities.Count == 0)
            return 0;

        context.ReportPresetAssignments.RemoveRange(entities);

        await context.SaveChangesAsync(
            cancellationToken);

        return entities.Count;
    }

    #region Mapping

    private static ReportPresetAssignment ToModel(
        ReportPresetAssignmentEntity entity)
    {
        return new ReportPresetAssignment
        {
            Id = entity.AssignmentId,

            ReportId =
                entity.ReportId,

            ContextType =
                entity.ContextType,

            ContextKey =
                entity.ContextKey,

            PresetId =
                entity.PresetId
        };
    }

    #endregion
}