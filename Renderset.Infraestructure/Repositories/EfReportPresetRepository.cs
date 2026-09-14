using Microsoft.EntityFrameworkCore;
using Renderset.Core.Configurations;
using Renderset.Core.Persistence;
using Renderset.Core.Presets;
using Renderset.Core.Themes;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfReportPresetRepository
    : IReportPresetRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

    public EfReportPresetRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ReportPreset?> GetByIdAsync(
        string tenantId,
        string presetId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportPresets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.PresetId == presetId &&
                        x.Active,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task<IReadOnlyCollection<ReportPreset>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.ReportPresets
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Active)
                .OrderBy(x => x.ReportId)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

        return entities
            .Select(ToModel)
            .ToList();
    }

    public async Task SaveAsync(
        string tenantId,
        ReportPreset preset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportPresets
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.PresetId == preset.Id,
                    cancellationToken);

        var configurationJson =
            JsonSerializer.Serialize(
                preset.Configuration,
                JsonOptions);

        var themeJson =
            JsonSerializer.Serialize(
                preset.Theme,
                JsonOptions);

        if (entity is null)
        {
            entity =
                new ReportPresetEntity
                {
                    TenantId = tenantId,
                    PresetId = preset.Id,

                    ReportId =
                        preset.Configuration.ReportId,

                    Name =
                        preset.Configuration.Name,

                    Version = preset.Version,

                    ConfigurationJson =
                        configurationJson,

                    ThemeJson =
                        themeJson,

                    Active = true,

                    CreatedAtUtc =
                        DateTime.UtcNow
                };

            context.ReportPresets.Add(entity);
        }
        else
        {
            entity.ReportId =
                preset.Configuration.ReportId;

            entity.Name =
                preset.Configuration.Name;

            entity.Version =
                preset.Version;

            entity.ConfigurationJson =
                configurationJson;

            entity.ThemeJson =
                themeJson;

            entity.Active = true;

            entity.UpdatedAtUtc =
                DateTime.UtcNow;
        }

        await context.SaveChangesAsync(
            cancellationToken);
    }

    #region Mapping

    private static ReportPreset ToModel(
        ReportPresetEntity entity)
    {
        var configuration =
            JsonSerializer.Deserialize<ReportConfiguration>(
                entity.ConfigurationJson,
                JsonOptions)
            ?? throw new InvalidOperationException(
                $"No se pudo deserializar la configuración del preset '{entity.PresetId}'.");

        var theme =
            JsonSerializer.Deserialize<ReportTheme>(
                entity.ThemeJson,
                JsonOptions)
            ?? throw new InvalidOperationException(
                $"No se pudo deserializar el tema del preset '{entity.PresetId}'.");

        return new ReportPreset
        {
            Id = entity.PresetId,
            Version = entity.Version,

            Configuration = configuration,
            Theme = theme
        };
    }

    #endregion
}