using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Renderset.Core.Configurations;
using Renderset.Core.Themes;
using Renderset.Deca.Issuing;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Deca;

namespace Renderset.Infrastructure.Repositories;

/// <summary>
/// Diseños del DeCA por tenant (base de datos Deca). Cada guardado inserta
/// una versión; la vigente es la mayor. Así cada DeCA puede decir con qué
/// diseño se emitió, y nadie pisa sin querer lo que otro acaba de guardar.
/// </summary>
public sealed class EfDecaTemplateRepository
    : IDecaTemplateRepository
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

    private readonly IDbContextFactory<DecaDbContext> _contextFactory;

    public EfDecaTemplateRepository(
        IDbContextFactory<DecaDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DecaTemplate?> GetCurrentAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var entity =
            await context.DecaTemplates
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
            return null;

        return new DecaTemplate
        {
            TenantId = entity.TenantId,
            Version = entity.Version,
            Configuration = entity.ConfigurationJson is null
                ? null
                : JsonSerializer.Deserialize<ReportConfiguration>(entity.ConfigurationJson, JsonOptions),
            Theme = entity.ThemeJson is null
                ? null
                : JsonSerializer.Deserialize<ReportTheme>(entity.ThemeJson, JsonOptions),
            Texts =
                JsonSerializer.Deserialize<Dictionary<string, string>>(entity.TextsJson, JsonOptions)
                ?? [],
            UpdatedAtUtc = DateTime.SpecifyKind(entity.UpdatedAtUtc, DateTimeKind.Utc),
            UpdatedBy = entity.UpdatedBy
        };
    }

    public async Task<bool> AddVersionAsync(
        DecaTemplate template,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        await using var context =
            await _contextFactory.CreateDbContextAsync(cancellationToken);

        var current =
            await context.DecaTemplates
                .Where(x => x.TenantId == template.TenantId)
                .MaxAsync(x => (int?)x.Version, cancellationToken)
            ?? DecaTemplate.BaseVersion;

        if (current != expectedVersion)
            return false;

        context.DecaTemplates.Add(
            new DecaTemplateEntity
            {
                TenantId = template.TenantId,
                Version = current + 1,
                ConfigurationJson = template.Configuration is null
                    ? null
                    : JsonSerializer.Serialize(template.Configuration, JsonOptions),
                ThemeJson = template.Theme is null
                    ? null
                    : JsonSerializer.Serialize(template.Theme, JsonOptions),
                TextsJson = JsonSerializer.Serialize(template.Texts, JsonOptions),
                UpdatedAtUtc = template.UpdatedAtUtc,
                UpdatedBy = template.UpdatedBy is { Length: > 200 } by ? by[..200] : template.UpdatedBy
            });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dos guardados a la vez con la misma versión esperada: la clave
            // (TenantId, Version) deja pasar sólo uno.
            return false;
        }

        return true;
    }
}
