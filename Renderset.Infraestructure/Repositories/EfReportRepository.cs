using Microsoft.EntityFrameworkCore;
using Renderset.Core.Definitions;
using Renderset.Core.Reports;
using Renderset.Infrastructure.Persistence;
using Renderset.Infrastructure.Persistence.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Renderset.Infrastructure.Repositories;

public sealed class EfReportRepository : IReportRepository
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

    public EfReportRepository(
        IDbContextFactory<RenderSetDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Report?> GetByIdAsync(
        string tenantId,
        string reportId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.Reports
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.ReportId == reportId &&
                        x.Active,
                    cancellationToken);

        return entity is null
            ? null
            : ToModel(entity);
    }

    public async Task<IReadOnlyCollection<Report>> GetAllAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entities =
            await context.Reports
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

    public async Task SaveAsync(
        string tenantId,
        Report report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (string.IsNullOrWhiteSpace(report.Id))
        {
            throw new ArgumentException(
                "Report.Id es obligatorio.",
                nameof(report));
        }

        await using var context =
            await _contextFactory.CreateDbContextAsync(
                cancellationToken);

        var entity =
            await context.Reports
                .FirstOrDefaultAsync(
                    x =>
                        x.TenantId == tenantId &&
                        x.ReportId == report.Id,
                    cancellationToken);

        var definitionJson =
            JsonSerializer.Serialize(
                report.Definition,
                JsonOptions);

        var schemaJson =
            JsonSerializer.Serialize(
                report.DataSchema,
                JsonOptions);

        var sampleDataJson =
            report.SampleData?.GetRawText();

        if (entity is null)
        {
            entity =
                new ReportEntity
                {
                    TenantId = tenantId,
                    ReportId = report.Id,

                    Name = report.Name,
                    Version = report.Version,

                    DefinitionJson = definitionJson,
                    SchemaJson = schemaJson,
                    SampleDataJson = sampleDataJson,

                    Active = true,

                    CreatedAtUtc =
                        DateTime.UtcNow
                };

            context.Reports.Add(entity);
        }
        else
        {
            entity.Name =
                report.Name;

            entity.Version =
                report.Version;

            entity.DefinitionJson =
                definitionJson;

            entity.SchemaJson =
                schemaJson;

            entity.SampleDataJson =
                sampleDataJson;

            entity.Active = true;

            entity.UpdatedAtUtc =
                DateTime.UtcNow;
        }

        await context.SaveChangesAsync(
            cancellationToken);
    }

    private static Report ToModel(
        ReportEntity entity)
    {
        var definition =
            JsonSerializer.Deserialize<ReportDefinition>(
                entity.DefinitionJson,
                JsonOptions)
            ?? throw new InvalidOperationException(
                $"No se ha podido deserializar DefinitionJson del report '{entity.ReportId}'.");

        var schema =
            JsonSerializer.Deserialize<ReportDataSchema>(
                entity.SchemaJson,
                JsonOptions)
            ?? throw new InvalidOperationException(
                $"No se ha podido deserializar SchemaJson del report '{entity.ReportId}'.");

        JsonElement? sampleData = null;

        if (!string.IsNullOrWhiteSpace(
                entity.SampleDataJson))
        {
            using var document =
                JsonDocument.Parse(
                    entity.SampleDataJson);

            sampleData =
                document.RootElement.Clone();
        }

        return new Report
        {
            Id = entity.ReportId,
            Name = entity.Name,
            Version = entity.Version,

            Definition = definition,
            DataSchema = schema,

            SampleData = sampleData
        };
    }
}