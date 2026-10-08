using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Renderset.Core.Themes;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfReportThemeRepository
    : IReportThemeRepository
{
    private readonly IDbContextFactory<RenderSetDbContext> _contextFactory;

    // Mismas opciones que ThemeJson en EfReportPresetRepository: el JSON de
    // un tema de empresa y el de la copia de un preset son intercambiables.
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

    public EfReportThemeRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyCollection<TenantReportTheme>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.ReportThemes
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.Active)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);

        return entities
            .Select(ToModel)
            .ToList();
    }

    public async Task<TenantReportTheme?> GetByIdAsync(
        string tenantId,
        string themeId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.ReportThemes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.ThemeId == themeId &&
                        x.Active,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task SaveAsync(
        string tenantId,
        TenantReportTheme theme,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(theme);

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        await using var transaction =
            await context.Database.BeginTransactionAsync(
                cancellationToken);

        var now = DateTime.UtcNow;

        if (theme.IsDefault)
        {
            await context.ReportThemes
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ThemeId != theme.Theme.Id &&
                    x.IsDefault)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.IsDefault, false)
                        .SetProperty(x => x.UpdatedAtUtc, now),
                    cancellationToken);
        }

        var entity =
            await context.ReportThemes
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.ThemeId == theme.Theme.Id,
                    cancellationToken);

        var json =
            JsonSerializer.Serialize(
                theme.Theme,
                JsonOptions);

        if (entity is null)
        {
            context.ReportThemes.Add(
                new ReportThemeEntity
                {
                    TenantId = tenantId,
                    ThemeId = theme.Theme.Id,
                    Name = theme.Theme.Name,
                    ThemeJson = json,
                    IsDefault = theme.IsDefault,
                    Active = true,
                    CreatedAtUtc = now
                });
        }
        else
        {
            // Un id dado de baja se puede volver a usar: se reactiva.
            entity.Name = theme.Theme.Name;
            entity.ThemeJson = json;
            entity.IsDefault = theme.IsDefault;
            entity.Active = true;
            entity.UpdatedAtUtc = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string tenantId,
        string themeId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var now = DateTime.UtcNow;

        var affected =
            await context.ReportThemes
                .Where(x =>
                    x.TenantId == tenantId &&
                    x.ThemeId == themeId &&
                    x.Active)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Active, false)
                        .SetProperty(x => x.IsDefault, false)
                        .SetProperty(x => x.UpdatedAtUtc, now),
                    cancellationToken);

        return affected > 0;
    }

    private static TenantReportTheme ToModel(
        ReportThemeEntity entity)
    {
        var theme =
            JsonSerializer.Deserialize<ReportTheme>(
                entity.ThemeJson,
                JsonOptions)
            ?? new ReportTheme();

        // La clave manda sobre lo que diga el JSON.
        theme.Id = entity.ThemeId;
        theme.Name = entity.Name;

        return new TenantReportTheme
        {
            Theme = theme,
            IsDefault = entity.IsDefault,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }
}
